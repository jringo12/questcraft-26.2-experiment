using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Networking;
using UnityEngine.Serialization;
using UnityEngine.UI;

public class InstanceManager : MonoBehaviour
{
    [SerializeField] private GameObject instanceArray;
    [SerializeField] private GameObject instancePrefab;
    [SerializeField] private TextMeshProUGUI instanceVersion;
    [SerializeField] private TextMeshProUGUI instanceTitle;
    [SerializeField] private RawImage instanceImage;
    [SerializeField] private GameObject instanceRemoveMenu;
    public TMP_InputField instanceName;
    public Toggle defaultModsToggle;
    public TMP_Dropdown versionDropdown;
    public TMP_Dropdown loaderDropdown;
    public WindowHandler windowHandler;
    public Texture2D errorTexture;

    [FormerlySerializedAs("ModCounter")] public TextMeshProUGUI modCounter;
    [SerializeField] private GameObject modPrefab;
    [SerializeField] private GameObject modArray;
    [SerializeField] private APIHandler apiHandler;
    // H6: per-instance renderer control, built once and reused. It is a plain
    // Button + TextMeshProUGUI created in code (no scene YAML surgery) and
    // parented under the info panel next to Update/Delete.
    private GameObject rendererButton;
    private TextMeshProUGUI rendererButtonLabel;
    private PojlibInstance currentInfoInstance;
    
    public void Start()
    {
        List<string> modLoaders = new List<string>
        {
            "Fabric",
            "Forge",
            "Quilt",
            "NeoForge"
        };
        
        loaderDropdown.AddOptions(modLoaders);
    }
    
    public void UpdateMenu()
    {
        string selectedModloader = loaderDropdown.options[loaderDropdown.value].text;
        defaultModsToggle.interactable = selectedModloader is "Fabric" or "Quilt";
        defaultModsToggle.isOn = selectedModloader is "Fabric" or "Quilt";
    }

    public void CreateCustomInstance()
    {
        try
        {
            string selectedModloader = loaderDropdown.options[loaderDropdown.value].text;

            instanceName.text = instanceName.text.Trim();
            if (instanceName.text == "")
            {
                JNIStorage.instance.uiHandler.SetAndShowError("Instance name cannot be blank, please enter a name.");
                return;
            }
            
            HashSet<string> instanceNames = new HashSet<string>
                (JNIStorage.instancesObj.Call<AndroidJavaObject[]>("toArray")
                    .Select(instance => PojlibInstance.Parse(instance).instanceName.ToLower()));

            if (instanceNames.Add(instanceName.text.ToLower()))
            {
                JNIStorage.apiClass.CallStatic<AndroidJavaObject>("createNewInstance", JNIStorage.activity, JNIStorage.instancesObj, instanceName.text, defaultModsToggle.isOn, versionDropdown.options[versionDropdown.value].text, selectedModloader, null);
                JNIStorage.instance.uiHandler.SetAndShowError(instanceName.text + " is now being created.");
            
                JNIStorage.instance.UpdateInstances();
            }
            else
                JNIStorage.instance.uiHandler.SetAndShowError("Instance already exists, please choose another name.");
        }
        catch (Exception e)
        {
            Debug.Log(e);
            throw;
        }
    }
    
    public void CreateInstanceArray()
    {
        ResetArray();

        foreach (var instanceObj in JNIStorage.instancesObj.Call<AndroidJavaObject[]>("toArray"))
        {
            async Task SetInstanceData()
            {
                PojlibInstance instance = PojlibInstance.Parse(instanceObj);
                GameObject instanceGameObject = Instantiate(instancePrefab, new Vector3(-10, -10, -10), Quaternion.identity);
                instanceGameObject.transform.GetChild(1).GetComponent<TextMeshProUGUI>().text = instance.instanceName; instanceGameObject.transform.GetChild(2).GetComponent<TextMeshProUGUI>().text = instance.versionName + " - Fabric";
                instanceGameObject.transform.SetParent(instanceArray.transform, false);
                instanceGameObject.name = instance.instanceName;

                instanceGameObject.GetComponent<Button>().onClick.AddListener(delegate
                {
                    EventSystem.current.SetSelectedGameObject(instanceGameObject);
                    GameObject instanceObject = GameObject.Find(EventSystem.current.currentSelectedGameObject.transform.name);
                    CreateInstanceInfoPage(instanceObject.name);
                });

                if (instance.instanceImageURL != null)
                {
                    apiHandler.DownloadImage(instance.instanceImageURL, instanceGameObject.GetComponentInChildren<RawImage>());
                }
            }
            SetInstanceData();
        }

        if (instanceArray.transform.childCount == 0)
        {
            GameObject instanceGameObject = Instantiate(instancePrefab, new Vector3(-10, -10, -10), Quaternion.identity);
            instanceGameObject.GetComponentInChildren<RawImage>().texture = errorTexture;
            instanceGameObject.GetComponentInChildren<RawImage>().color = Color.yellow;
            instanceGameObject.transform.GetChild(1).GetComponent<TextMeshProUGUI>().text = "No instances could be found!";
            instanceGameObject.transform.GetChild(2).GetComponent<TextMeshProUGUI>().text = "Make sure you have an instance downloaded.";
            instanceGameObject.transform.SetParent(instanceArray.transform, false);
            instanceGameObject.name = "ERROR";
        }
    }

    async void CreateInstanceInfoPage(string slug)
    {
        PojlibInstance instance = JNIStorage.GetInstance(slug);
        windowHandler.InstanceInfoSetter();

        instanceVersion.text = instance.versionName + " - Fabric";
        instanceTitle.text = instance.instanceName;

        // H6: remember which instance this page is showing, and (re)build the
        // per-instance renderer control for it.
        currentInfoInstance = instance;
        SetupRendererButton();

        if (instance.instanceImageURL != null)
            apiHandler.DownloadImage(instance.instanceImageURL, instanceImage);

        for (int i = modArray.transform.childCount - 1; i >= 0; i--)
            Destroy(modArray.transform.GetChild(i).gameObject);

        PojlibMod[] modList = instance.GetMods();

        void CountMods(int count)
        {
            Color counterColor;
            if (count < 50)
                counterColor = Color.Lerp(new Color(3, 152, 252), new Color(255, 108, 10), (float)count / 50);
            else if (count == 69)
                counterColor = Color.green;
            else
                counterColor = Color.Lerp(new Color(255, 108, 10), new Color(255, 0, 0), (count - 50) / 50.0f);
            modCounter.text = $"Currently Installed mods: <color={ColorToHex(counterColor)}>{count}";
        }

        CountMods(modList.Length);

        List<MetaParser> fetchedMods = new();

        async Task GetModInfoArray()
        {
            string request = "https://api.modrinth.com/v2/projects?ids=[";
            foreach (PojlibMod mod in modList)
                request += $"\"{mod.slug}\",";
            request = request.Remove(request.Length - 1, 1) + "]";

            UnityWebRequest www = UnityWebRequest.Get(request);
            www.SetRequestHeader("User-Agent", "QuestCraftPlusPlus/QuestCraft/" + Application.version + " (discord.gg/questcraft)");
            www.SendWebRequest();
            while (!www.isDone)
                await Task.Delay(16);
            if (www.result != UnityWebRequest.Result.Success)
                Debug.Log(www.error);
            else
                fetchedMods =
                    new List<MetaParser>(JsonConvert.DeserializeObject<MetaParser[]>(www.downloadHandler.text));

            HashSet<string> fetchedSlugs = new HashSet<string>(fetchedMods.Select(mod => mod.slug.ToLower()));
            foreach (PojlibMod mod in modList)
            {
                string currentSlug = mod.slug.ToLower();
                if (fetchedSlugs.Add(currentSlug))
                {
                    MetaParser newMod = new MetaParser
                    {
                        slug = currentSlug,
                        title = currentSlug,
                        description = "",
                        icon_url = "OFFLINE"
                    };
                    fetchedMods.Add(newMod);
                }
            }
        }

        await GetModInfoArray();

        //sort it
        fetchedMods = fetchedMods.OrderBy(mod => mod.title).ToList();

        foreach (MetaParser mod in fetchedMods)
        {
            GameObject modObject = Instantiate(modPrefab, new Vector3(-10, -10, -10), Quaternion.identity);
            modObject.name = mod.slug;
            modObject.transform.SetParent(modArray.transform, false);
            modObject.GetComponent<RectTransform>().sizeDelta =
                new(675, modObject.GetComponent<RectTransform>().sizeDelta.y);
            modObject.transform.GetChild(3).GetComponent<Button>().onClick.AddListener(delegate
            {
                bool deleted = JNIStorage.apiClass.CallStatic<bool>("removeExtraProject", JNIStorage.instancesObj, instance.raw, mod.slug);
                if (deleted)
                {
                    Destroy(modObject.gameObject);
                    CountMods(modArray.transform.childCount - 1);
                }
                else
                {
                    JNIStorage.instance.uiHandler.SetAndShowError("Failed to delete this mod, is it a core mod?");
                }
            });

            modObject.transform.GetChild(1).GetComponent<TextMeshProUGUI>().text = mod.title;
            modObject.transform.GetChild(2).GetComponent<TextMeshProUGUI>().text = mod.description;
            if (mod.icon_url != "OFFLINE")
            {
                apiHandler.DownloadImage(mod.icon_url, modObject.GetComponentInChildren<RawImage>());
            }
        }
    }

    // H6: per-instance renderer control. A tap cycles Vulkan <-> OpenGL and
    // persists the choice via pojlib (instances.json). The button is created
    // once and reused across info-page opens; it lives under the info panel
    // so it hides with the panel. Label shows the live value.
    private void SetupRendererButton()
    {
        if (currentInfoInstance == null || instanceTitle == null)
            return;

        if (rendererButton == null)
        {
            Transform parent = instanceTitle.transform.parent;
            if (parent == null)
                return;

            rendererButton = new GameObject("RendererButton");
            RectTransform rt = rendererButton.AddComponent<RectTransform>();
            rt.SetParent(parent, false);
            // Match Update/Delete buttons: centred anchors, top-left pivot,
            // same size, stacked just below Delete.
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(436.4f, -202.6f);
            rt.sizeDelta = new Vector2(213.2f, 40f);

            Image bg = rendererButton.AddComponent<Image>();
            // Borrow the Delete button's sprite so the control matches the
            // panel's style; fall back to a plain white image.
            Transform deleteButton = parent.Find("DeleteButton");
            Sprite buttonSprite = deleteButton != null ? deleteButton.GetComponent<Image>()?.sprite : null;
            bg.sprite = buttonSprite;
            bg.color = Color.white;

            Button button = rendererButton.AddComponent<Button>();
            button.targetGraphic = bg;
            button.onClick.AddListener(OnRendererButtonClicked);

            GameObject labelObj = new GameObject("Text (TMP)");
            RectTransform labelRt = labelObj.AddComponent<RectTransform>();
            labelRt.SetParent(rendererButton.transform, false);
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.offsetMin = Vector2.zero;
            labelRt.offsetMax = Vector2.zero;

            rendererButtonLabel = labelObj.AddComponent<TextMeshProUGUI>();
            // Match the Delete button's label styling.
            TextMeshProUGUI deleteLabel = deleteButton != null
                ? deleteButton.Find("Text (TMP)")?.GetComponent<TextMeshProUGUI>()
                : null;
            if (deleteLabel != null)
            {
                rendererButtonLabel.font = deleteLabel.font;
                rendererButtonLabel.fontSize = deleteLabel.fontSize;
                rendererButtonLabel.color = deleteLabel.color;
                rendererButtonLabel.alignment = deleteLabel.alignment;
            }
            else
            {
                rendererButtonLabel.fontSize = 24;
                rendererButtonLabel.color = Color.black;
                rendererButtonLabel.alignment = TextAlignmentOptions.Center;
            }
        }

        RefreshRendererButtonLabel();
    }

    private void RefreshRendererButtonLabel()
    {
        if (rendererButtonLabel != null && currentInfoInstance != null)
        {
            string name = currentInfoInstance.GetRenderer() == "opengl" ? "OpenGL" : "Vulkan";
            rendererButtonLabel.text = "Renderer: " + name + " (tap to change)";
        }
    }

    private void OnRendererButtonClicked()
    {
        if (currentInfoInstance == null)
            return;

        string next = currentInfoInstance.GetRenderer() == "opengl" ? "vulkan" : "opengl";
        bool saved = JNIStorage.apiClass.CallStatic<bool>("setInstanceRenderer", JNIStorage.instancesObj, currentInfoInstance.raw, next);
        if (!saved)
        {
            JNIStorage.instance.uiHandler.SetAndShowError("Failed to save renderer choice.");
            return;
        }

        currentInfoInstance.renderer = next;
        RefreshRendererButtonLabel();
    }

    string ColorToHex(Color color)
    {
        int r = (int)color.r;
        int g = (int)color.g;
        int b = (int)color.b;
        return "#" + r.ToString("X2") + g.ToString("X2") + b.ToString("X2");
    }

    public void RemoveInstancePrompt()
    {
        instanceRemoveMenu.GetComponentInChildren<TextMeshProUGUI>().text = "Pressing continue will delete this instance forever! Are you sure you want to do this?";
        instanceRemoveMenu.SetActive(true);
    }

    public void RemoveInstance()
    {
        JNIStorage.apiClass.CallStatic<bool>("deleteInstance", JNIStorage.instancesObj, JNIStorage.GetInstance(instanceTitle.text).raw);
        instanceRemoveMenu.SetActive(false);
        windowHandler.InstanceInfoUnsetter();
    }

    private void ResetArray()
    {
        for (int i = instanceArray.transform.childCount - 1; i >= 0; i--)
        {
            Destroy(instanceArray.transform.GetChild(i).gameObject);
        }
    }
}