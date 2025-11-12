// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using System;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using System.Collections;

namespace realvirtual
{
    [Serializable]
  public class UISceneElement : MonoBehaviour, IPointerClickHandler,IPointerEnterHandler,IPointerExitHandler
  {
      public Texture2D Icon;
      public TextMeshProUGUI SceneName;
      public TextMeshProUGUI Description;
      public GameObject HightlightPic;
      public string scenePath;
      [ReadOnly]public string assetBundle="";
     
      public Image img;

      [ReadOnly] public GameObject Menu;
      [ReadOnly] public SceneManagement SceneManagement;
      public void Init()
      {
         if(Icon!=null)
            img.sprite = Sprite.Create(Icon, new Rect(0, 0, Icon.width, Icon.height), new Vector2(0.5f, 0.5f));
      }
      public void SetSceneName(string name)
      {
          SceneName.text = name;
      }
      public void SetDescription(string description)
      {
          Description.text = description;
      }
      public void OnPointerClick(PointerEventData eventData)
      {
          LoadScene();
      }

      public void LoadScene()
      {
         Menu.SetActive(false);
          if(!string.IsNullOrEmpty(assetBundle))
          {
              var streamingAssetsPath = Application.streamingAssetsPath+"/realvirtual/scenes";
              string[] assetBundleFiles = Directory.GetFiles(streamingAssetsPath, "*", SearchOption.AllDirectories);
              var sceneName = Path.GetFileNameWithoutExtension(scenePath);
              foreach (string file in assetBundleFiles)
              {
                  if (Path.GetExtension(file) == ".realvirtual") // Unity AssetBundles have no extension
                  {
                      AssetBundle ab = AssetBundle.LoadFromFile(file);
                      string[] scenePaths = ab.GetAllScenePaths();
                      if (scenePaths.Contains(scenePath))
                      {
                          SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
                          SceneManagement.currentloadedScene = sceneName;
                          SceneManagement.currentAssetBundle = ab;
                          break;
                      }
                  }
              }
          }
          else
          {
              string sceneName = Path.GetFileNameWithoutExtension(scenePath);
              SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
              SceneManagement.currentloadedScene = sceneName;
          }
          Menu.SetActive(false); 
      }


      public void OnPointerEnter(PointerEventData eventData)
      {
          HightlightPic.SetActive(true);
      }

      public void OnPointerExit(PointerEventData eventData)
      {
          HightlightPic.SetActive(false);
      }
      
  }
}

