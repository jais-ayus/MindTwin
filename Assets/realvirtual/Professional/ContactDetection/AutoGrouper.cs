#if REALVIRTUAL_BURST
// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2024 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

namespace realvirtual
{
    public class AutoGrouper : MonoBehaviour
    {
        public string seperatingGroupName;
        public string groupPrefix;


        [Button("Clear Groups")]
        public void Clear()
        {
            ClearGroups();
        }

        [Button("Auto Group")]
        public void AutoGroup()
        {
            ClearGroups();

            var targets = GetComponentsInChildren<MeshFilter>();
            var processed = new List<MeshFilter>();

            var groupCount = 0;
            var groupName = "";

            for (var i = 0; i < targets.Length; i++)
            {
                var target = targets[i];
                var group = target.GetComponent<Group>();
                if (group == null)
                {
                    groupName = groupPrefix + " Group " + groupCount;


                    var connected = ContactDetection.FindAllConnected(target.gameObject);

                    //Debug.Log("Found " + connected.Count + " object for group " + groupName);


                    for (var j = 0; j < connected.Count; j++)
                    {
                        var newGroup = connected[j].AddComponent<Group>();
                        newGroup.GroupName = groupName;
                    }


                    groupCount += 1;
                }
            }
        }

        private void ClearGroups()
        {
            var targets = GetComponentsInChildren<Group>(true);

            for (var i = 0; i < targets.Length; i++)
            {
                var group = targets[i];
                if (group.GroupName != seperatingGroupName) DestroyImmediate(group);
            }
        }
    }
}

#endif