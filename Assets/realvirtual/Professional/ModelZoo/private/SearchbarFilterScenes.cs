// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace realvirtual
{
  public class SearchbarFilterScenes : MonoBehaviour
  {
      public TMP_InputField inputField;
      public GameObject contentRoot;

      public void FilterElements()
      {
          var filter = inputField.text;
          
          var children = new List<Transform>();
          foreach (Transform child in contentRoot.transform) 
              children.Add(child);
          
          if (string.IsNullOrEmpty(filter))
          {
              foreach (var child in children) 
                  child.gameObject.SetActive(true);
              return;
          }

          foreach (var child in children)
          {
              // Check for UISceneElement component
              UISceneElement sceneElement = child.gameObject.GetComponent<UISceneElement>();
              bool matchesSceneElement = false;

              if (sceneElement != null)
              {
                  // Compare filter against two parameters of UISceneElement
                  matchesSceneElement = sceneElement.SceneName.text.ToLower().Contains(filter.ToLower()) ||
                                        sceneElement.Description.text.ToLower().Contains(filter.ToLower());
              }

              // Apply filtering
              child.gameObject.SetActive(matchesSceneElement);
          }
      }
  }
}

