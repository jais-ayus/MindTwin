#if REALVIRTUAL_BURST
// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2024 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  


using UnityEngine;
using NaughtyAttributes;

namespace realvirtual
{
    public class TestNeighbourSearch : MonoBehaviour
    {
        [Button("TestAll")]
        void TestAll(){

            GameObject target = gameObject;

            ContactDetection.FindContacts(target, null, 0, true, 1);

        }
    }
}

#endif