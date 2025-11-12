// realvirtual.io (formerly game4automation) (R) a Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// (c) 2025 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/unternehmen/lizenz  

using System.Collections.Generic;
using UnityEngine;


namespace realvirtual
{
    public class SignalState
    {
        public List<bool> values = new List<bool>();
        
        public SignalState(List<Signal> signals)
        {
            foreach (Signal signal in signals)
            {
                if (signal is PLCInputBool || signal is PLCOutputBool)
                {
                    values.Add((bool)signal.GetValue());
                }

            }
        }

        public bool IsEqual(SignalState other)
        {
            if (values.Count != other.values.Count)
            {
                return false;
            }

            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] != other.values[i])
                {
                    return false;
                }
            }

            return true;
        }

    }
}
