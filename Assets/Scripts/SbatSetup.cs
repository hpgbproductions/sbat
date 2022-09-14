using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public class SbatSetup : MonoBehaviour
{
    static public string BatchFolderPath;

    private string HelloWorldPath;
    private string HelloWorldName = "HELLO.SB";
    private string HelloWorldContents = @"REM ""This is a test Simple Batch Program""
VAR %1, MyName
LOG ""Hello, %MyName%!""";

    private void Awake()
    {
        BatchFolderPath = Path.Combine(Application.persistentDataPath, "NACHSAVE", "SBAT");
        Directory.CreateDirectory(BatchFolderPath);

        HelloWorldPath = Path.Combine(BatchFolderPath, HelloWorldName);
        if (!File.Exists(HelloWorldPath))
        {
            using (StreamWriter sw = new StreamWriter(File.Create(HelloWorldPath)))
            {
                sw.Write(HelloWorldContents);
            }
        }
    }
}
