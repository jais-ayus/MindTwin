// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2024 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

#region

using System.Collections.Generic;
using NaughtyAttributes;
using UnityEngine;

#endregion

namespace realvirtual
{
    public class PartListGrouper : MonoBehaviour
    {
        public string filePath;
        public string groupName;

        public List<string> IDs;


        [Button("Load IDs")]
        private void LoadIDs()
        {
            IDs = GetMaterialNames();
        }


        [Button("Group")]
        public void Group()
        {
            var transforms = GetComponentsInChildren<Transform>();

            Debug.Log("Checking " + transforms.Length + " parts against " + IDs.Count + " ids");

            var matches = new List<GameObject>();


            var idHash = new HashSet<string>(IDs);

            for (var i = 0; i < transforms.Length; i++)
            {
                var target = transforms[i];
                var name = target.gameObject.name;

                for (var j = 0; j < IDs.Count; j++)
                {
                    var id = IDs[j];

                    if (name.StartsWith(id))
                    {
                        if (name.Length > id.Length)
                        {
                            var c = name[id.Length];
                            if (!char.IsDigit(c))
                            {
                                matches.Add(target.gameObject);
                            }
                        }
                        else
                        {
                            matches.Add(target.gameObject);
                        }
                    }
                }
            }
            
            for (var i = 0; i < matches.Count; i++)
            {
                var obj = matches[i];
                var group = obj.GetComponent<Group>();
                if (group == null) group = obj.AddComponent<Group>();

                group.GroupName = groupName;
            }
        }

        public List<string> GetMaterialNames()
        {
            return CSVReader.ReadFirstColumn(filePath, ';', true);
        }
    }
}