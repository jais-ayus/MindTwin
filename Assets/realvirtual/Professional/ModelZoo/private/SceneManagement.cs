// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;
using TMPro;
using UnityEngine.PlayerLoop;
using UnityEngine.Serialization;
using UnityEngine.UI;

namespace realvirtual
{
  public class SceneManagement : MonoBehaviour
  {
        public GameObject SceneRowPrefab;
        public RectTransform contentPanel;
        public GameObject MenuCanvas;
        public GameObject LoadingCanvas;
        public GameObject LoadAnimation;
        public RuntimeNews runtimeNews;
        [ReadOnly] public List<SceneDescription> Scenes= new List<SceneDescription>();
        [ReadOnly] public List<string> scenePaths = new List<string>();
        [ReadOnly] public string currentloadedScene;
        [ReadOnly] public AssetBundle currentAssetBundle;
        

        private string streamingAssetsPath;
        private bool WindowActive;
        private List<string> sceneList=new List<string>();
        private rvSceneDescriptions sceneDescriptionsSO;
        private string ZooScene;

        private void Awake()
        {
            // check if gameobject already exists
            var objs = FindObjectsByType(GetType(), FindObjectsSortMode.None);
            if (objs.Length > 1)
            {
                Destroy(gameObject);
            }
            DontDestroyOnLoad(gameObject);
        }

        public void Start()
        {
            int sceneCount = SceneManager.sceneCountInBuildSettings;
            for (int i = 1; i < sceneCount; i++)
            {
                var path = SceneUtility.GetScenePathByBuildIndex(i);
                sceneList.Add(path);
            }
            // find scriptable object of type rvSceneDescriptions
            sceneDescriptionsSO = UnityEngine.Resources.Load<rvSceneDescriptions>("SceneDescriptions");
            scenePaths = new List<string>();
            streamingAssetsPath = Application.streamingAssetsPath+"/realvirtual/scenes";
            MenuCanvas.SetActive(false);
            LoadingCanvas.SetActive(true);
            LoadAnimation.SetActive(true);
            ZooScene=SceneManager.GetActiveScene().name;
            StartCoroutine(LoadSceneAsync());
        }

        public void activateLoadScreen(bool activate)
        {
            if (activate)
            {
                MenuCanvas.SetActive(false);
                LoadingCanvas.SetActive(true);
                LoadAnimation.SetActive(true);
            }
            else
            {
                MenuCanvas.SetActive(true);
                LoadingCanvas.SetActive(false);
                LoadAnimation.SetActive(false);
            
            }
        }
        public void Update()
        {
            
            if (Input.GetKeyDown(KeyCode.M) && !MenuCanvas.activeSelf)
            {
                StartCoroutine(UnloadScene());
            }
            if(Input.GetKey("escape") && !MenuCanvas.activeSelf )
            {
                StartCoroutine(UnloadScene());
            }
        }
        private IEnumerator UnloadScene()
        {
            if(currentAssetBundle!=null)
                currentAssetBundle.Unload(true);
            currentAssetBundle = null;
            currentloadedScene = "";
            var debugConsole = GameObject.Find("DebugConsole");
            if(debugConsole!=null)
                Destroy(debugConsole);
            SceneManager.LoadScene(ZooScene, LoadSceneMode.Single);
            yield return new WaitForSeconds(0.2f);
            MenuCanvas.SetActive(true);
            LoadingCanvas.SetActive(false);
            LoadAnimation.SetActive(false);
        }
        private IEnumerator LoadSceneAsync()
        {
            
            foreach (var scene in sceneList)
            {
                if(sceneDescriptionsSO.SceneDescriptionsList.Exists(x=>x.sceneName==Path.GetFileNameWithoutExtension(scene)))
                {
                    SceneDescriptionSO sceneDescriptionSO = sceneDescriptionsSO.SceneDescriptionsList.Find(x => x.sceneName == Path.GetFileNameWithoutExtension(scene));
                    SceneDescription sceneDescription = new SceneDescription();
                    sceneDescription.DisplayName = sceneDescriptionSO.DisplayName;
                    sceneDescription.Description = sceneDescriptionSO.Description;
                    sceneDescription.SceneIcon = sceneDescriptionSO.SceneIcon;
                    sceneDescription.sceneName = Path.GetFileNameWithoutExtension(scene);
                    Scenes.Add(sceneDescription);
                    scenePaths.Add(scene);
                }
                else
                {
                    string scenePath = scene;
                    if (!string.IsNullOrEmpty(scenePath))
                    {
                        string sceneName = Path.GetFileNameWithoutExtension(scenePath);
                        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

                        while (!asyncLoad.isDone)
                        {
                            yield return null;
                        }
                        
                        Scene sceneasset = SceneManager.GetSceneByName(sceneName);
                        DisableSceneUI(sceneasset);
                        SceneDescription sceneDescription = SearchForComponent(sceneasset);
                        if (sceneDescription != null)
                        {
                            Scenes.Add(sceneDescription);
                            scenePaths.Add(scenePath);
                            sceneDescription.assetBundle = "";
                        }

                        yield return new WaitForSeconds(1f); // Allow time before unloading
                        SceneManager.UnloadSceneAsync(sceneName);
                    }
                }
            }
            // Check scenes in Streaming Assets
            if(Directory.Exists(streamingAssetsPath))
            {
                string[] assetBundleFiles = Directory.GetFiles(streamingAssetsPath, "*", SearchOption.AllDirectories);
                foreach (string file in assetBundleFiles)
                {
                    if (Path.GetExtension(file) == ".realvirtual") // Unity AssetBundles have no extension
                    {
                        AssetBundle assetBundle = AssetBundle.LoadFromFile(file);
                        if (assetBundle == null)
                        {
                            continue;
                        }

                        string[] scenePaths = assetBundle.GetAllScenePaths();
                        foreach (string scenePath in scenePaths)
                        {
                            yield return StartCoroutine(LoadSceneAndCheckComponent(scenePath, assetBundle));
                        }

                        assetBundle.Unload(false); // Keep loaded assets, but unload bundle
                    }
                }

                if (Scenes.Count == 0)
                    yield return null;
            }
            LoadScenes();
        }
        private IEnumerator LoadSceneAndCheckComponent(string scenePath, AssetBundle assetBundle)
        {
            string sceneName = Path.GetFileNameWithoutExtension(scenePath);
            AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName, LoadSceneMode.Additive);

            while (!asyncLoad.isDone)
            {
                yield return null;
            }
            // Check for the SceneDescription component
            Scene loadedScene = SceneManager.GetSceneByName(sceneName);
            if (loadedScene.isLoaded)
            {
                bool found = false;
                DisableSceneUI(loadedScene);
                var objs = loadedScene.GetRootGameObjects();
                foreach (GameObject obj in objs)
                {
                    if (obj.GetComponent<SceneDescriptionComponent>() != null)
                    {
                        SceneDescription sceneDescription = SearchForComponent(loadedScene);
                        if (sceneDescription != null)
                        {
                            if (scenePaths.Contains(scenePath))
                            {
                                for (int i = 0; i < Scenes.Count; i++)
                                {
                                    if (Scenes[i].sceneName == sceneDescription.sceneName)
                                    {
                                        sceneDescription.assetBundle = assetBundle.ToString();
                                        Scenes[i] = sceneDescription;
                                        continue;
                                    }
                                }
                                for (int i = 0; i < scenePaths.Count; i++)
                                {
                                    if (scenePaths[i] == scenePath)
                                    {
                                        scenePaths[i] = scenePath;
                                        continue;
                                    }
                                }
                            }
                            else
                            {
                                Scenes.Add(sceneDescription);
                                sceneDescription.assetBundle = assetBundle.ToString();
                                scenePaths.Add(scenePath);
                            }
                            found = true;
                        }
                        break;
                    }
                }
                if (!found)
                {
                    Debug.LogWarning($"SceneDescription not found in {sceneName}");
                }
                yield return new WaitForSeconds(1f); // Allow time before unloading
                SceneManager.UnloadSceneAsync(sceneName);
            }
        }
        private SceneDescription SearchForComponent(Scene scene)
        {
            GameObject[] allComponents = scene.GetRootGameObjects();
            SceneDescriptionComponent sceneDescription = new SceneDescriptionComponent();
            foreach (var comp in allComponents)
            {
                if (comp.GetComponent<SceneDescriptionComponent>())
                {
                   sceneDescription = comp.GetComponent<SceneDescriptionComponent>();
                }
            }
            return sceneDescription.sceneDescription;;
        }
      private void DisableSceneUI(Scene scene)
      {
          GameObject[] allComponents = scene.GetRootGameObjects();
          foreach (var comp in allComponents)
          {
              if (comp.name == "realvirtual")
              {
                    GameObject UI = comp.transform.Find("UI").gameObject;
                    if (UI != null)
                    {
                        UI.SetActive(false);
                    }
              }
              else
              {
                  if (comp.GetComponent<Canvas>())
                  {
                      comp.GetComponent<Canvas>().enabled = false;
                  }
                  else
                  {
                      var canvasList = comp.GetComponentsInChildren<Canvas>();
                      if(canvasList.Length>0)
                          foreach (var canvas in canvasList)
                          {
                              canvas.enabled = false;
                          }
                  }
              }
              
          }
      }
      private void LoadScenes()
      {
          Debug.Log("Load Scenes");
          foreach (string scenePath in scenePaths)
          {
              CreateSceneButton(scenePath);
          }
          MenuCanvas.SetActive(true);
          LoadingCanvas.SetActive(false);
          LoadAnimation.SetActive(false);
          if(runtimeNews!=null)
          {
              runtimeNews.ShowNews();
          }
      }
      void CreateSceneButton(string scenePath)
      {
          GameObject newElement = Instantiate(SceneRowPrefab, contentPanel);
          UISceneElement sceneElement = newElement.GetComponent<UISceneElement>();
          string sceneName = Path.GetFileNameWithoutExtension(scenePath);
          SceneDescription sceneDescription=GetSceneDescription(sceneName);
          sceneElement.SetSceneName(sceneDescription.DisplayName);
          sceneElement.SetDescription(sceneDescription.Description);
          sceneElement.Icon = sceneDescription.SceneIcon;
          sceneElement.scenePath = scenePath;
          sceneElement.SceneManagement = this;
          sceneElement.assetBundle = sceneDescription.assetBundle;
          sceneElement.Menu = MenuCanvas;
          sceneElement.Init();
      }
      private SceneDescription GetSceneDescription(string sceneName)
      {
          var sceneDescription=new SceneDescription();
          foreach (var scene in Scenes)
          {
              if(scene.sceneName==sceneName)
                  sceneDescription = scene;
          }
          return sceneDescription;
      }
      
  }
}

