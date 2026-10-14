using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;

public class ConfigHandler : MonoBehaviour
{
    public TMP_Dropdown accountsDropdown;
    public WindowHandler windowHandler;
    public DevHandler devHandler;
    public Config config;
    public static string configPath;

    // H2 FIX: single null-tolerant config loader. A missing, empty, or corrupt
    // launcher.conf yields valid defaults (and the file is repaired) instead of
    // a null Config that NREs every caller. All config reads go through here so
    // no caller can deserialize a half-written file into a null.
    public static Config LoadConfigFile()
    {
        if (string.IsNullOrEmpty(configPath))
            configPath = Application.persistentDataPath + "/launcher.conf";

        Config loaded = null;
        try
        {
            if (File.Exists(configPath))
            {
                string configFile = File.ReadAllText(configPath);
                if (!string.IsNullOrWhiteSpace(configFile))
                    loaded = JsonConvert.DeserializeObject<Config>(configFile);
            }
        }
        catch (Exception e)
        {
            // Unreadable or corrupt config: fall through to defaults below.
            // A broken launcher.conf must never crash startup (H2).
            Debug.LogWarning("QCXR: launcher.conf unreadable/corrupt, using defaults: " + e.Message);
        }

        if (loaded == null)
        {
            // H2 FIX: never leave an empty/corrupt file behind — write valid
            // defaults so the next read succeeds even if this write is the
            // only thing that runs before a crash.
            loaded = DefaultConfig();
            try
            {
                File.WriteAllText(configPath, JsonConvert.SerializeObject(loaded, Formatting.Indented));
            }
            catch (IOException e)
            {
                Debug.LogWarning("QCXR: could not repair launcher.conf: " + e.Message);
            }
        }

        // Normalise fields that older/corrupt configs may leave null.
        if (loaded.accounts == null)
            loaded.accounts = new List<Accounts>();
        if (loaded.customRAMValue == null)
            loaded.customRAMValue = "2048";

        return loaded;
    }

    private static Config DefaultConfig()
    {
        return new Config
        {
            acceptedLegal = false,
            setDevMods = false,
            setCustomRAM = false,
            customRAMValue = "2048",
            lastSelectedInstance = 0,
            lastSelectedAccount = 0,
            accounts = new List<Accounts>()
        };
    }

    // H2 FIX: save helper — every mutation loads through LoadConfigFile()
    // (null-tolerant) so a missing/empty/corrupt file can never NRE a setter.
    private void SaveConfig(Config toSave)
    {
        if (string.IsNullOrEmpty(configPath))
            configPath = Application.persistentDataPath + "/launcher.conf";
        File.WriteAllText(configPath, JsonConvert.SerializeObject(toSave, Formatting.Indented));
        config = toSave;
    }

    public void LoadConfig()
    {
        // H2 FIX: null-tolerant load; config is never null after this.
        config = LoadConfigFile();

        if (!config.acceptedLegal) { windowHandler?.LegalSetter(); }
        if (config.setDevMods && devHandler != null) { devHandler._devToggle.isOn = true; }
        if (config.setCustomRAM && devHandler != null)
        {
            devHandler._ramSetterToggle.isOn = true;
            devHandler._ramSetterField.text = config.customRAMValue;
            JNIStorage.apiClass.SetStatic("memoryValue", config.customRAMValue);
        }

        if (JNIStorage.instance != null && JNIStorage.instance.instancesDropdown != null)
            JNIStorage.instance.instancesDropdown.value = config.lastSelectedInstance;
    }

    public void SetLastSelectedInstance(int instance)
    {
        Config loaded = LoadConfigFile();
        loaded.lastSelectedInstance = instance;
        SaveConfig(loaded);
    }

    public void SetLastSelectedAccount(int account)
    {
        Config loaded = LoadConfigFile();
        loaded.lastSelectedAccount = account;
        SaveConfig(loaded);
    }

    public void SetMemoryValue()
    {
        Config loaded = LoadConfigFile();
        string ramValue = devHandler != null ? devHandler._ramSetterField.text : null;

        if (ramValue != null)
        {
            loaded.customRAMValue = ramValue;
            SaveConfig(loaded);
            JNIStorage.apiClass.SetStatic("memoryValue", ramValue);
        }
    }

    public void AgreeToLegal()
    {
        Config loaded = LoadConfigFile();
        loaded.acceptedLegal = true;
        SaveConfig(loaded);
    }

    public class Config
    {
        public bool acceptedLegal;
        public bool setDevMods;
        public bool setCustomRAM;
        public string customRAMValue;
        public int lastSelectedInstance;
        public int lastSelectedAccount;
        public List<Accounts> accounts;
    }

    public class Accounts
    {
        public string username;
        public string uuid;
    }
}
