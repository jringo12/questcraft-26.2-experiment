using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.Serialization;

public class LoginHandler : MonoBehaviour
{
    bool isLoggedIn;

    [SerializeField] private TMP_Dropdown accountDropdown;
    public List<ConfigHandler.Accounts> accounts = new();
    private Dictionary<string, string> accountUUIDMap = new();
    private string configPath;
    public GameObject loginText;
    public WindowHandler handler;
    public UIHandler UIHandler;
    public bool isDemoMode;

    public string selectedAccountUsername = "Add Account";
    private string selectedAccountUUID;
    public SkinHandler skinHandler;
    private ConfigHandler.Config config;

    public void Start()
    {
	    configPath = Application.persistentDataPath + "/launcher.conf";
	    // H2 FIX: NEVER create an empty config file. An empty launcher.conf
	    // deserializes to null and NREs ParseAccounts, bricking the launcher
	    // until app data is cleared. LoadConfigFile() returns valid defaults
	    // (and repairs the file) for a missing/empty/corrupt config.
	    config = ConfigHandler.LoadConfigFile();
	    ParseAccounts();
	    Login();
    }

    public void Login()
    {
	    if (Application.platform != RuntimePlatform.Android) return;
	    // H2 FIX: guard the dropdown — a stray/unwired LoginHandler (or a
	    // scene missing the dropdown) must not NRE here.
	    if (accountDropdown == null || accountDropdown.options == null || accountDropdown.options.Count == 0)
	    {
		    Debug.LogWarning("QCXR: Login skipped, account dropdown not wired.");
		    return;
	    }
		isLoggedIn = false;
	    int selectedIndex = Mathf.Clamp(accountDropdown.value, 0, accountDropdown.options.Count - 1);
	    selectedAccountUsername = accountDropdown.options[selectedIndex].text;
	    if (!accountUUIDMap.TryGetValue(selectedAccountUsername, out selectedAccountUUID))
	    {
		    Debug.LogWarning("QCXR: No UUID mapped for account " + selectedAccountUsername);
		    selectedAccountUUID = null;
	    }
	    Debug.Log(selectedAccountUUID);
	    if (loginText != null)
		    loginText.SetActive(true);
	    JNIStorage.apiClass.CallStatic("login", JNIStorage.activity, selectedAccountUsername == "Add Account" ? null : selectedAccountUUID);
	    CheckVerification();
    }

    public void ParseAccounts()
    {
	    accounts.Clear();
	    accountUUIDMap.Clear();

	    // H2 FIX: config/accounts may be null if this instance was created
	    // before the fix or the config was corrupt; reload defaults instead
	    // of NREing on config.accounts.
	    if (config == null)
		    config = ConfigHandler.LoadConfigFile();
	    if (config.accounts == null)
		    config.accounts = new List<ConfigHandler.Accounts>();

	    int i = 0;
	    foreach (var account in config.accounts.Where(account => account != null))
	    {
		    Debug.Log("Account " + i++);
		    accounts.Add(account);
		    accountUUIDMap[account.username] = account.uuid;
		    Debug.Log("Account " + i++ + " added.");
	    }

	    Debug.Log("Accounts parsed.");
	    ConfigHandler.Accounts addAccount = new ConfigHandler.Accounts { username = "Add Account", uuid = "null" };

	    if (accounts.Count == 0)
	    {
		    Debug.Log("No accounts, using account sign in.");
		    accounts.Add(addAccount);
		    accountUUIDMap[addAccount.username] = addAccount.uuid;
		    if (accountDropdown != null)
		    {
			    accountDropdown.ClearOptions();
			    accountDropdown.AddOptions(accounts.Select(account => account.username).ToList());
		    }
		    Login();
	    }
	    else
	    {
		    accounts.Add(addAccount);
		    accountUUIDMap[addAccount.username] = addAccount.uuid;
		    if (accountDropdown != null)
		    {
			    accountDropdown.ClearOptions();
			    accountDropdown.AddOptions(accounts.Select(account => account.username).ToList());
		    }
	    }
    }

    private async void CheckVerification() {
	    if (Application.platform != RuntimePlatform.Android) return;
	    while (!isLoggedIn)
	    {
		    await Task.Delay(1500);

		    Debug.Log("Check Login State");
		    if (JNIStorage.accountObj != JNIStorage.apiClass.GetStatic<AndroidJavaObject>("currentAcc"))
		    {
			    JNIStorage.accountObj = JNIStorage.apiClass.GetStatic<AndroidJavaObject>("currentAcc");
			    if (JNIStorage.accountObj != null)
			    {
				    string accName = JNIStorage.apiClass.GetStatic<string>("profileName");
				    string accUUID = JNIStorage.apiClass.GetStatic<string>("profileUUID");
				    Debug.Log("Logged Into " + accName + " | " + accUUID);
				    if (loginText != null)
					    loginText.SetActive(false);
				    isLoggedIn = true;
				    // H2 FIX: null-tolerant reload instead of raw deserialize.
				    config = ConfigHandler.LoadConfigFile();
				    bool accExists = config.accounts.Any(account => account != null && account.username == accName);
				    if (config.accounts.All(account => account == null || account.uuid != accUUID))
				    {
					    config.accounts.Add(new ConfigHandler.Accounts { username = accName, uuid = accUUID });
				    }

				    config.lastSelectedAccount = accountDropdown != null ? accountDropdown.value : 0;
				    string JSON = JsonConvert.SerializeObject(config, Formatting.Indented);
				    File.WriteAllText(configPath, JSON);
				    isDemoMode = JNIStorage.apiClass.GetStatic<bool>("isDemoMode");
				    if (!accExists)
				    {
					    ParseAccounts();
				    }

				    selectedAccountUsername = accName;
				    selectedAccountUUID = accUUID;
				    if (handler != null)
				    {
					    if (accName != "Add Account")
					    {
						    handler.LoadAv(selectedAccountUsername);
					    }
					    else
					    {
						    handler.LoadAv(accName);
					    }
				    }
			    }
		    }
	    }
    }

    public void RemoveAccount()
    {
	    JNIStorage.accountObj = null;
	    JNIStorage.apiClass.CallStatic<bool>("removeAccount", JNIStorage.activity, selectedAccountUUID);

	    // H2 FIX: null-tolerant reload instead of raw deserialize.
	    config = ConfigHandler.LoadConfigFile();

	    config.accounts.RemoveAll(account => account != null && account.username == selectedAccountUsername);
	    config.lastSelectedAccount = 0;
	    string JSON = JsonConvert.SerializeObject(config, Formatting.Indented);
	    File.WriteAllText(configPath, JSON);

	    if (accountDropdown != null)
	    {
		    accountDropdown.ClearOptions();
		    accountDropdown.AddOptions(accounts.Select(account => account.username).ToList());
	    }
	    if (accounts.Count > 0)
		    selectedAccountUsername = accounts.First().username;

	    handler?.LogoutWindowUnsetter();
	    ParseAccounts();
	    if (handler != null)
		    handler.LoadAv(selectedAccountUsername);
    }
}
