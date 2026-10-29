using Esper.ESave;
using Esper.ESave.Encryption;
using System;
using UnityEngine;
using UnityEngine.InputSystem;
using static Esper.ESave.SaveFileSetupData;

public class SaveFileManager : MonoBehaviour
{
    public static SaveFileManager Instance;

    public string saveFileName = "GameSave";
    public SaveLocation saveLocation = SaveLocation.PersistentDataPath;
    public string saveFolder = "Saves";
    public FileType fileType = FileType.Json;
    public EncryptionMethod encryption = EncryptionMethod.None;
    public string aesKey = "";
    public string aesIV = "";
    public bool useBackgroundTask = false;
    public bool addToStorage = true;

    private SaveFile currentSaveFile;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            InitializeSaveFile();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void InitializeSaveFile()
    {
        SaveFileSetupData setupData = new SaveFileSetupData(
            fileName: saveFileName,
            saveLocation: saveLocation,
            filePath: saveFolder,
            fileType: fileType,
            encryptionMethod: encryption,
            aesKey: aesKey,
            aesIV: aesIV,
            addToStorage: addToStorage,
            backgroundTask: useBackgroundTask
        );

        currentSaveFile = new SaveFile(setupData, shouldExist: false);
    }

    public void SaveData<T>(string dataID, T data)
    {
        if (currentSaveFile == null)
        {
            Debug.LogError("存档文件未初始化！");
            return;
        }

        if (data is Vector3 v3)
        {
            SaveData(dataID + "_x", v3.x);
            SaveData(dataID + "_y", v3.y);
            SaveData(dataID + "_z", v3.z);
            return;
        }
        currentSaveFile.AddOrUpdateData(dataID, data);
        currentSaveFile.Save();
    }

    public T LoadData<T>(string dataID, T defaultValue = default)
    {
        if (currentSaveFile == null)
        {
            Debug.LogError("存档文件未初始化！");
            return defaultValue;
        }

        if (typeof(T) == typeof(Vector3))
        {
            //把上层传入的defaultValue作为兜底
            Vector3 defaultVec = (Vector3)(object)defaultValue;
            float x = LoadData<float>(dataID + "_x", defaultVec.x);
            float y = LoadData<float>(dataID + "_y", defaultVec.y);
            float z = LoadData<float>(dataID + "_z", defaultVec.z);
            object vec = new Vector3(x, y, z);
            return (T)vec;
        }


        if (!currentSaveFile.HasData(dataID))
        {
            return defaultValue;
        }
        return currentSaveFile.GetData<T>(dataID, defaultValue);
    }

    public void DeleteData(string dataID)
    {
        if (currentSaveFile == null) return;
        currentSaveFile.DeleteData(dataID);
        currentSaveFile.Save();
    }

    public void ClearAllData()
    {
        if (currentSaveFile == null) return;
        currentSaveFile.EmptyFile();
    }

    public void DeleteSaveFile()
    {
        if (currentSaveFile == null) return;
        currentSaveFile.DeleteFile();
    }

    public string[] GetAllStoredDataIDs()
    {
        if (currentSaveFile == null)
        {
            Debug.LogError("存档文件未初始化！");
            return Array.Empty<string>();
        }
        return currentSaveFile.GetAllDataIDs();
    }
}