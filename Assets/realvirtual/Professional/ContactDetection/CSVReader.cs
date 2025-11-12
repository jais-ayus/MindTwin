// realvirtual (R) Framework for Automation Concept Design, Virtual Commissioning and 3D-HMI
// Copyright(c) 2019 realvirtual GmbH - Usage of this source code only allowed based on License conditions see https://realvirtual.io/en/company/license

namespace realvirtual
{
    using System;
    using System.Collections.Generic;
    using System.IO;

    public class CSVReader
    {
        public static List<string> ReadFirstColumn(string filePath, char delimiter, bool hasHeader)
        {
            List<string> firstColumnValues = new List<string>();

            try
            {
                using (StreamReader sr = new StreamReader(filePath))
                {
                    string line;
                    if (hasHeader)
                    {
                        line = sr.ReadLine();
                    }

                    while ((line = sr.ReadLine()) != null)
                    {
                        // Split the line by comma
                        string[] values = line.Split(delimiter);

                        // Ensure there is at least one column in the line
                        if (values.Length > 0)
                        {
                            firstColumnValues.Add(values[0]);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("The file could not be read:");
                Console.WriteLine(e.Message);
            }

            return firstColumnValues;
        }
    }
}
