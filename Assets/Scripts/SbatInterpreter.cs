using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Reflection;
using UnityEngine;

public class SbatInterpreter : MonoBehaviour
{
    // Program memory
    /*
     * 100 register
     * 101 date string
     * 102 time string
     * 103 is in designer
     * 104 is in level
     * 105 level name
     * 106 map name
     */
    private object[] Memory = new object[107];
    private string[] VarNames = new string[100];
    private object Register
    {
        get { return Memory[100]; }
        set { Memory[100] = value; }
    }
    private int VarNamesUsed = 0;

    // Regex
    private Regex ReadArgsStringRegex;
    private Regex ReadSbatLineRegex;

    // Read console feature
    private Component DevConsoleComponent;
    private List<object> LogEntriesList;
    private TypeInfo DevConsoleTypeInfo;
    private MethodInfo ExecuteCommandInfo;
    private FieldInfo LogEntriesFieldInfo;
    private TypeInfo LogEntryTypeInfo;
    private FieldInfo LogTypeFieldInfo;         // UnityEngine.LogType LogType
    private FieldInfo LogMessageInfo;           // string Message
    private FieldInfo LogMessageDetailsInfo;    // string MessageDetails

    private void Awake()
    {
        ServiceProvider.Instance.DevConsole.RegisterCommand<string>("ExecuteSB", ExecuteBatch);
        ServiceProvider.Instance.DevConsole.RegisterCommand("PrintSBMemory", PrintMemory);

        ReadArgsStringRegex = new Regex(@"^(?>[\s]*?((?("").+?""|[\S-[,]]+))\s*?(?>,|$)){1,10}",
            RegexOptions.Compiled | RegexOptions.Singleline,
            new System.TimeSpan(0, 0, 1));
        ReadSbatLineRegex = new Regex(@"^[\s]*?([A-Za-z]+)[\s]*?(?>[\s]*?((?("").+?""|[\S-[,]]+))\s*?(?>,|$))*",
            RegexOptions.Compiled | RegexOptions.Singleline,
            new System.TimeSpan(0, 0, 1));
    }

    private void ExecuteBatch(string argsStringOriginal)
    {
        if (LogEntryTypeInfo == null)
        {
            GetDevConsoleInfo();
        }

        System.Diagnostics.Stopwatch sw = new System.Diagnostics.Stopwatch();
        sw.Start();

        List<object> args = GetArgsFromString(argsStringOriginal, 10);

        string StartMessage = $"Executing Simple Batch Program: {args[0]}";
        for (int a = 1; a < args.Count; a++)
        {
            StartMessage += $"\n%{a:D1}: [{args[a].GetType()}] {args[a]}";
        }
        Debug.Log(StartMessage);

        // BEGIN Simple Batch Interpreter

        // Program memory
        List<string> ProgramLines;
        int NumLines = 0;
        int CurrentLine = 0;

        bool Echo = true;

        try
        {
            ProgramLines = File.ReadLines(Path.Combine(SbatSetup.BatchFolderPath, args[0] + ".SB")).ToList();
            NumLines = ProgramLines.Count;
        }
        catch (Exception ex)
        {
            Debug.LogErrorFormat("Failed to load Simple Batch File: {0}.SB \n{1}", args[0], ex);
            return;
        }

        // Initialize memory arrays
        VarNamesUsed = 10;
        for (int i = 0; i < 10; i++)
        {
            Memory[i] = args[i];
            VarNames[i] = "";
        }
        for (int i = 10; i < 100; i++)
        {
            Memory[i] = 0;
            VarNames[i] = i.ToString();
        }
        // Apply special values
        Memory[100] = 0;
        Memory[101] = DateTime.Now.ToLongDateString();
        Memory[102] = DateTime.Now.ToLongTimeString();
        Memory[103] = ServiceProvider.Instance.GameState.IsInDesigner ? 1 : 0;
        Memory[104] = ServiceProvider.Instance.GameState.IsInLevel ? 1 : 0;
        Memory[105] = ServiceProvider.Instance.GameState.CurrentLevelName;
        Memory[106] = ServiceProvider.Instance.GameState.CurrentMapName;

        while (CurrentLine < NumLines)
        {
            if (TryReadSbatLine(ProgramLines[CurrentLine], out string Command, out List<object> CommandArgs))
            {
                Command.ToUpperInvariant();

                // Command memory
                object left = 0;
                object right = 0;
                object value = 0;

                // Execute the detected command
                switch (Command)
                {
                    case "VAR":
                        // Declare a new variable
                        // VAR (string varName)
                        // VAR (object value, string varName)
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        else if (VarNamesUsed >= 100)
                        {
                            Debug.LogError("Cannot declare new variable: Memory array full");
                            return;
                        }
                        else if (CommandArgs.Count == 1)
                        {
                            if (CommandArgs[0].ToString()[0] == '%' || char.IsDigit(CommandArgs[0].ToString()[0]))
                            {
                                Debug.LogError("Cannot declare new variable: Cannot start name with a percent sign or digit");
                                return;
                            }
                            Memory[VarNamesUsed] = 0;
                            VarNames[VarNamesUsed] = CommandArgs[0].ToString();
                            VarNamesUsed++;
                        }
                        else
                        {
                            if (CommandArgs[1].ToString()[0] == '%' || char.IsDigit(CommandArgs[1].ToString()[0]))
                            {
                                Debug.LogError("Cannot declare new variable: Cannot start name with a percent sign or digit");
                                return;
                            }
                            Memory[VarNamesUsed] = ParseCommandArg(CommandArgs[0]);
                            VarNames[VarNamesUsed] = CommandArgs[1].ToString();
                            VarNamesUsed++;
                        }
                        break;

                    case "ST":
                    case "SET":
                        // Set the value of the register, or the variable at the address
                        // ST (object value)
                        // ST (object value, int addr)
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        else if (CommandArgs.Count == 1)
                            Register = ParseCommandArg(CommandArgs[0]);
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[1])] = ParseCommandArg(CommandArgs[0]);
                        break;

                    case "STC":
                        // Set the value of the register, or the variable at the address
                        // STC (string value)
                        // STC (string value, int addr)
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        else if (CommandArgs.Count == 1)
                            Register = ParseCommandArg(CommandArgs[0], true);
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[1])] = ParseCommandArg(CommandArgs[0], true);
                        break;

                    case "TOINT":
                    case "FLR":
                        // FLR (float value)
                        // FLR (float value, int addr)
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        else if (CommandArgs.Count == 1)
                            Register = Mathf.FloorToInt((float)ParseCommandArg(CommandArgs[0]));
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[1])] = Mathf.FloorToInt((float)ParseCommandArg(CommandArgs[0]));
                        break;

                    case "RND":
                        // Round to the nearest integer
                        // RND (float value)
                        // RND (float value, int addr)
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        else if (CommandArgs.Count == 1)
                            Register = Mathf.RoundToInt((float)ParseCommandArg(CommandArgs[0]));
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[1])] = Mathf.RoundToInt((float)ParseCommandArg(CommandArgs[0]));
                        break;

                    case "CEIL":
                        // CEIL (float value)
                        // CEIL (float value, int addr)
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        else if (CommandArgs.Count == 1)
                            Register = Mathf.CeilToInt((float)ParseCommandArg(CommandArgs[0]));
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[1])] = Mathf.CeilToInt((float)ParseCommandArg(CommandArgs[0]));
                        break;

                    case "CHAR":
                        // Get a single character string. The character corresponds to the provided value.
                        // CHAR (int value)
                        // CHAR (int value, int addr)
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        else if (CommandArgs.Count == 1)
                            Register = ((char)(int)ParseCommandArg(CommandArgs[0])).ToString();
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[1])] = ((char)(int)ParseCommandArg(CommandArgs[0])).ToString();
                        break;

                    case "PARSE":
                        // Read a number provided as a string. May output an integer or floating-point number.
                        // PARSE (string str)
                        // PARSE (string str, int addr)
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        else if (int.TryParse(ParseCommandArg(CommandArgs[0], true).ToString(), out int parsedInt))
                        {
                            if (CommandArgs.Count == 1)
                                Register = parsedInt;
                            else
                                Memory[(int)ParseCommandArg(CommandArgs[1])] = parsedInt;
                        }
                        else if (float.TryParse(ParseCommandArg(CommandArgs[0], true).ToString(), out float parsedFlt))
                        {
                            if (CommandArgs.Count == 1)
                                Register = parsedFlt;
                            else
                                Memory[(int)ParseCommandArg(CommandArgs[1])] = parsedFlt;
                        }
                        else
                        {
                            Debug.LogError($"Failed to parse the string as a number: {ParseCommandArg(CommandArgs[0], true)}");
                            return;
                        }
                        break;

                    case "STR":
                        // Convert a number into a string
                        // STR (void)
                        // STR (object value)
                        // STR (object value, int addr)
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        else if (CommandArgs.Count == 1)
                            Register = ParseCommandArg(CommandArgs[0]).ToString();
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[1])] = ParseCommandArg(CommandArgs[0]).ToString();
                        break;

                    case "HEX":
                        // Get the hexadecimal representation of an integer
                        // HEX (void)
                        // HEX (object value)
                        // HEX (object value, int addr)
                        if (CommandArgs.Count == 0)
                            left = Register;
                        else
                            left = ParseCommandArg(CommandArgs[0]);
                        if (left.GetType() == typeof(int))
                            value = Convert.ToString((int)left, 16).ToUpperInvariant();
                        else
                            throw new ArgumentException($"Cannot convert value of type {left.GetType()} to hexadecimal notation");
                        if (CommandArgs.Count < 2)
                            Register = value;
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[1])] = value;
                        break;

                    case "BIN":
                        // Get the binary representation of an integer
                        // BIN (void)
                        // BIN (object value)
                        // BIN (object value, int addr)
                        if (CommandArgs.Count == 0)
                            left = Register;
                        else
                            left = ParseCommandArg(CommandArgs[0]);
                        if (left.GetType() == typeof(int))
                            value = Convert.ToString((int)left, 2);
                        else
                            throw new ArgumentException($"Cannot convert value of type {left.GetType()} to binary notation");
                        if (CommandArgs.Count < 2)
                            Register = value;
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[1])] = value;
                        break;

                    case "ADD":
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        else if (CommandArgs.Count == 1)
                        {
                            left = Register;
                            right = ParseCommandArg(CommandArgs[0]);
                        }
                        else
                        {
                            left = ParseCommandArg(CommandArgs[0]);
                            right = ParseCommandArg(CommandArgs[1]);
                        }
                        if (left.GetType() == typeof(string) || right.GetType() == typeof(string))
                            value = left.ToString() + right.ToString();
                        else if (left.GetType() == typeof(float) || right.GetType() == typeof(float))
                            value = ToFloat(left) + ToFloat(right);
                        else
                            value = (int)left + (int)right;
                        if (CommandArgs.Count < 3)
                            Register = value;
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[2])] = value;
                        break;

                    case "SUB":
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        else if (CommandArgs.Count == 1)
                        {
                            left = Register;
                            right = ParseCommandArg(CommandArgs[0]);
                        }
                        else
                        {
                            left = ParseCommandArg(CommandArgs[0]);
                            right = ParseCommandArg(CommandArgs[1]);
                        }
                        if (left.GetType() == typeof(float) || right.GetType() == typeof(float))
                            value = ToFloat(left) - ToFloat(right);
                        else if (left.GetType() == typeof(int) && right.GetType() == typeof(int))
                            value = (int)left - (int)right;
                        else
                            throw new ArgumentException($"SUB cannot be applied to operands of type {left.GetType()} and {right.GetType()}");
                        if (CommandArgs.Count < 3)
                            Register = value;
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[2])] = value;
                        break;

                    case "MUL":
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        else if (CommandArgs.Count == 1)
                        {
                            left = Register;
                            right = ParseCommandArg(CommandArgs[0]);
                        }
                        else
                        {
                            left = ParseCommandArg(CommandArgs[0]);
                            right = ParseCommandArg(CommandArgs[1]);
                        }
                        if (left.GetType() == typeof(string) && right.GetType() == typeof(int))
                        {
                            if ((int)right < 1)
                                throw new ArgumentOutOfRangeException("Cannot multiply a string by zero or a negative number");
                            StringBuilder sb = new StringBuilder();
                            for (int i = 0; i < (int)right; i++)
                                sb.Append((string)left);
                            value = sb.ToString();
                        }
                        else if (left.GetType() == typeof(float) || right.GetType() == typeof(float))
                            value = ToFloat(left) * ToFloat(right);
                        else if (left.GetType() == typeof(int) && right.GetType() == typeof(int))
                            value = (int)left * (int)right;
                        else
                            throw new ArgumentException($"Cannot multiply values of type {left.GetType()} and {right.GetType()}");
                        if (CommandArgs.Count < 3)
                            Register = value;
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[2])] = value;
                        break;

                    case "DIV":
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        else if (CommandArgs.Count == 1)
                        {
                            left = Register;
                            right = ParseCommandArg(CommandArgs[0]);
                        }
                        else
                        {
                            left = ParseCommandArg(CommandArgs[0]);
                            right = ParseCommandArg(CommandArgs[1]);
                        }
                        if (left.GetType() == typeof(float) || right.GetType() == typeof(float))
                            value = ToFloat(left) / ToFloat(right);
                        else if (left.GetType() == typeof(int) && right.GetType() == typeof(int))
                            value = (int)left / (int)right;
                        else
                            throw new ArgumentException($"Cannot multiply values of type {left.GetType()} and {right.GetType()}");
                        if (CommandArgs.Count < 3)
                            Register = value;
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[2])] = value;
                        break;

                    case "MOD":
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        else if (CommandArgs.Count == 1)
                        {
                            left = Register;
                            right = ParseCommandArg(CommandArgs[0]);
                        }
                        else
                        {
                            left = ParseCommandArg(CommandArgs[0]);
                            right = ParseCommandArg(CommandArgs[1]);
                        }
                        if (left.GetType() == typeof(float) || right.GetType() == typeof(float))
                            value = Mathf.Repeat(ToFloat(left), ToFloat(right));
                        else if (left.GetType() == typeof(int) && right.GetType() == typeof(int))
                            value = (int)left % (int)right;
                        else
                            throw new ArgumentException($"Cannot apply the modulus function on values of type {left.GetType()} and {right.GetType()}");
                        if (CommandArgs.Count < 3)
                            Register = value;
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[2])] = value;
                        break;

                    case "RANDOM":
                        // Returns a random floating-point number between 0 and 1
                        // RANDOM (void)
                        // RANDOM (int addr)
                        // Returns a random number using UnityEngine.Random.Range
                        // RANDOM (float minInclusive, float maxInclusive)
                        // RANDOM (float minInclusive, float maxInclusive, int addr)
                        // RANDOM (int minInclusive, int maxExclusive)
                        // RANDOM (int minInclusive, int maxExclusive, int addr)
                        if (CommandArgs.Count == 0)
                            Register = UnityEngine.Random.Range(0f, 1f);
                        else if (CommandArgs.Count == 1)
                            Memory[(int)ParseCommandArg(CommandArgs[0])] = UnityEngine.Random.Range(0f, 1f);
                        else
                        {
                            left = ParseCommandArg(CommandArgs[0]);
                            right = ParseCommandArg(CommandArgs[1]);
                            if (left.GetType() == typeof(float) || right.GetType() == typeof(float))
                                value = UnityEngine.Random.Range(ToFloat(left), ToFloat(right));
                            else if (left.GetType() == typeof(int) && right.GetType() == typeof(int))
                                value = UnityEngine.Random.Range((int)left, (int)right);
                            else
                                throw new ArgumentException($"Cannot apply the random function on values of type {left.GetType()} and {right.GetType()}");
                            if (CommandArgs.Count < 3)
                                Register = value;
                            else
                                Memory[(int)ParseCommandArg(CommandArgs[2])] = value;
                        }
                        break;

                    case "SHL":
                        // Bitwise shift left <<
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        else if (CommandArgs.Count == 1)
                        {
                            left = Register;
                            right = ParseCommandArg(CommandArgs[0]);
                        }
                        else
                        {
                            left = ParseCommandArg(CommandArgs[0]);
                            right = ParseCommandArg(CommandArgs[1]);
                        }
                        if (left.GetType() == typeof(int) && right.GetType() == typeof(int))
                            value = (int)left << (int)right;
                        else
                            throw new ArgumentException($"Cannot apply bit shift on values of type {left.GetType()} and {right.GetType()}");
                        if (CommandArgs.Count < 3)
                            Register = value;
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[2])] = value;
                        break;

                    case "SHR":
                        // Bitwise shift right >>
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        else if (CommandArgs.Count == 1)
                        {
                            left = Register;
                            right = ParseCommandArg(CommandArgs[0]);
                        }
                        else
                        {
                            left = ParseCommandArg(CommandArgs[0]);
                            right = ParseCommandArg(CommandArgs[1]);
                        }
                        if (left.GetType() == typeof(int) && right.GetType() == typeof(int))
                            value = (int)left >> (int)right;
                        else
                            throw new ArgumentException($"Cannot apply bit shift on values of type {left.GetType()} and {right.GetType()}");
                        if (CommandArgs.Count < 3)
                            Register = value;
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[2])] = value;
                        break;

                    case "INOT":
                        // Bitwise NOT (complement)
                        if (CommandArgs.Count == 0)
                            left = Register;
                        else
                            left = ParseCommandArg(CommandArgs[0]);
                        if (left.GetType() == typeof(int))
                            value = ~(int)left;
                        else
                            throw new ArgumentException($"Cannot apply bitwise operators on values of type {left.GetType()}");
                        if (CommandArgs.Count < 2)
                            Register = value;
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[1])] = value;
                        break;

                    case "IAND":
                        // Bitwise logical AND
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        else if (CommandArgs.Count == 1)
                        {
                            left = Register;
                            right = ParseCommandArg(CommandArgs[0]);
                        }
                        else
                        {
                            left = ParseCommandArg(CommandArgs[0]);
                            right = ParseCommandArg(CommandArgs[1]);
                        }
                        if (left.GetType() == typeof(int) && right.GetType() == typeof(int))
                            value = (int)left & (int)right;
                        else
                            throw new ArgumentException($"Cannot apply bitwise operators on values of type {left.GetType()} and {right.GetType()}");
                        if (CommandArgs.Count < 3)
                            Register = value;
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[2])] = value;
                        break;

                    case "IOR":
                        // Bitwise logical OR
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        else if (CommandArgs.Count == 1)
                        {
                            left = Register;
                            right = ParseCommandArg(CommandArgs[0]);
                        }
                        else
                        {
                            left = ParseCommandArg(CommandArgs[0]);
                            right = ParseCommandArg(CommandArgs[1]);
                        }
                        if (left.GetType() == typeof(int) && right.GetType() == typeof(int))
                            value = (int)left | (int)right;
                        else
                            throw new ArgumentException($"Cannot apply bitwise operators on values of type {left.GetType()} and {right.GetType()}");
                        if (CommandArgs.Count < 3)
                            Register = value;
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[2])] = value;
                        break;

                    case "IXOR":
                        // Bitwise logical XOR
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        else if (CommandArgs.Count == 1)
                        {
                            left = Register;
                            right = ParseCommandArg(CommandArgs[0]);
                        }
                        else
                        {
                            left = ParseCommandArg(CommandArgs[0]);
                            right = ParseCommandArg(CommandArgs[1]);
                        }
                        if (left.GetType() == typeof(int) && right.GetType() == typeof(int))
                            value = (int)left ^ (int)right;
                        else
                            throw new ArgumentException($"Cannot apply bitwise operators on values of type {left.GetType()} and {right.GetType()}");
                        if (CommandArgs.Count < 3)
                            Register = value;
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[2])] = value;
                        break;

                    case "ISTRUE":
                        // Returns the value according to the IsArgTrue function
                        if (CommandArgs.Count == 0)
                            left = Register;
                        else
                            left = ParseCommandArg(CommandArgs[0]);
                        value = IsArgTrue(left) ? 1 : 0;
                        if (CommandArgs.Count < 2)
                            Register = value;
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[1])] = value;
                        break;

                    case "ISFALSE":
                    case "NOT":
                        if (CommandArgs.Count == 0)
                            left = Register;
                        else
                            left = ParseCommandArg(CommandArgs[0]);
                        value = !IsArgTrue(left) ? 1 : 0;
                        if (CommandArgs.Count < 2)
                            Register = value;
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[1])] = value;
                        break;

                    case "AND":
                        // Boolean logical AND
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        else if (CommandArgs.Count == 1)
                        {
                            left = Register;
                            right = ParseCommandArg(CommandArgs[0]);
                        }
                        else
                        {
                            left = ParseCommandArg(CommandArgs[0]);
                            right = ParseCommandArg(CommandArgs[1]);
                        }
                        value = IsArgTrue(left) && IsArgTrue(right) ? 1 : 0;
                        if (CommandArgs.Count < 3)
                            Register = value;
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[2])] = value;
                        break;

                    case "OR":
                        // Boolean logical OR
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        else if (CommandArgs.Count == 1)
                        {
                            left = Register;
                            right = ParseCommandArg(CommandArgs[0]);
                        }
                        else
                        {
                            left = ParseCommandArg(CommandArgs[0]);
                            right = ParseCommandArg(CommandArgs[1]);
                        }
                        value = IsArgTrue(left) || IsArgTrue(right) ? 1 : 0;
                        if (CommandArgs.Count < 3)
                            Register = value;
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[2])] = value;
                        break;

                    case "XOR":
                        // Boolean logical XOR
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        else if (CommandArgs.Count == 1)
                        {
                            left = Register;
                            right = ParseCommandArg(CommandArgs[0]);
                        }
                        else
                        {
                            left = ParseCommandArg(CommandArgs[0]);
                            right = ParseCommandArg(CommandArgs[1]);
                        }
                        value = IsArgTrue(left) ^ IsArgTrue(right) ? 1 : 0;
                        if (CommandArgs.Count < 3)
                            Register = value;
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[2])] = value;
                        break;

                    case "STRGET":
                        // Get an integer corresponding to the character at the index
                        // STRGET (int index)
                        // STRGET (string str, int index)
                        // STRGET (string str, int index, int addr)
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        else if (CommandArgs.Count == 1)
                        {
                            left = Register;
                            right = ParseCommandArg(CommandArgs[0]);
                        }
                        else
                        {
                            left = ParseCommandArg(CommandArgs[0], true);
                            right = ParseCommandArg(CommandArgs[0]);
                        }
                        if (right.GetType() == typeof(int))
                            value = left.ToString()[(int)right];
                        else
                            throw new ArgumentException($"Cannot get character with values of type {left.GetType()} and {right.GetType()}");
                        if (CommandArgs.Count < 3)
                            Register = value;
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[2])] = value;
                        break;

                    case "STRLEN":
                        // Outputs the length of the string
                        // STRLEN (void)
                        // STRLEN (string str)
                        // STRLEN (string str, int addr)
                        if (CommandArgs.Count == 0)
                            Register = Register.ToString().Length;
                        else if (CommandArgs.Count == 1)
                            Register = ParseCommandArg(CommandArgs[0], true).ToString().Length;
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[1])] = ParseCommandArg(CommandArgs[0], true).ToString().Length;
                        break;

                    case "SUBSTR":
                        // Outputs a substring using a provided start point and length
                        // SUBSTR (int start, int length)
                        // SUBSTR (string str, int start, int length)
                        // SUBSTR (string str, int start, int length, int addr)
                        if (CommandArgs.Count < 2)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        else if (CommandArgs.Count == 2)
                        {
                            value = Register;
                            left = ParseCommandArg(CommandArgs[0]);
                            right = ParseCommandArg(CommandArgs[1]);
                        }
                        else
                        {
                            value = ParseCommandArg(CommandArgs[0], true);
                            left = ParseCommandArg(CommandArgs[1]);
                            right = ParseCommandArg(CommandArgs[2]);
                        }
                        if (left.GetType() == typeof(int) && right.GetType() == typeof(int))
                            value = ((string)value).Substring((int)left, (int)right);
                        else
                            throw new ArgumentException($"Cannot get substring with values of type {left.GetType()} and {right.GetType()}");
                        if (CommandArgs.Count < 4)
                            Register = value;
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[3])] = value;
                        break;

                    case "ISEQ":
                        // Outputs 1 if both values are equal, otherwise outputs 0
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        else if (CommandArgs.Count == 1)
                        {
                            left = Register;
                            right = ParseCommandArg(CommandArgs[0]);
                        }
                        else
                        {
                            left = ParseCommandArg(CommandArgs[0]);
                            right = ParseCommandArg(CommandArgs[1]);
                        }
                        if (left.GetType() == typeof(string) || right.GetType() == typeof(string))
                            value = left.ToString() == right.ToString();
                        else
                            value = ToFloat(left) == ToFloat(right) ? 1 : 0;
                        if (CommandArgs.Count < 3)
                            Register = value;
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[2])] = value;
                        break;

                    case "ISLT":
                        // Outputs 1 if left is smaller than right, otherwise outputs 0
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        else if (CommandArgs.Count == 1)
                        {
                            left = Register;
                            right = ParseCommandArg(CommandArgs[0]);
                        }
                        else
                        {
                            left = ParseCommandArg(CommandArgs[0]);
                            right = ParseCommandArg(CommandArgs[1]);
                        }
                        if (left.GetType() == typeof(string) || right.GetType() == typeof(string))
                            throw new ArgumentException($"Cannot compare values of type {left.GetType()} and {right.GetType()}");
                        else
                            value = ToFloat(left) < ToFloat(right) ? 1 : 0;
                        if (CommandArgs.Count < 3)
                            Register = value;
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[2])] = value;
                        break;

                    case "ISGT":
                        // Outputs 1 if left is larger than right, otherwise outputs 0
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        else if (CommandArgs.Count == 1)
                        {
                            left = Register;
                            right = ParseCommandArg(CommandArgs[0]);
                        }
                        else
                        {
                            left = ParseCommandArg(CommandArgs[0]);
                            right = ParseCommandArg(CommandArgs[1]);
                        }
                        if (left.GetType() == typeof(string) || right.GetType() == typeof(string))
                            throw new ArgumentException($"Cannot compare values of type {left.GetType()} and {right.GetType()}");
                        else
                            value = ToFloat(left) > ToFloat(right) ? 1 : 0;
                        if (CommandArgs.Count < 3)
                            Register = value;
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[2])] = value;
                        break;

                    case "TYPINT":
                        // Outputs 1 if the input is of type int, otherwise outputs 0
                        // TYPINT (void)
                        // TYPINT (object input)
                        // TYPINT (object input, int addr)
                        if (CommandArgs.Count == 0)
                            Register = (Register.GetType() == typeof(int)) ? 1 : 0;
                        else if (CommandArgs.Count == 1)
                            Register = (ParseCommandArg(CommandArgs[0]).GetType() == typeof(int)) ? 1 : 0;
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[1])] = (ParseCommandArg(CommandArgs[0]).GetType() == typeof(int)) ? 1 : 0;
                        break;

                    case "TYPFLT":
                        // Outputs 1 if the input is of type float, otherwise outputs 0
                        // TYPFLT (void)
                        // TYPFLT (object input)
                        // TYPFLT (object input, int addr)
                        if (CommandArgs.Count == 0)
                            Register = (Register.GetType() == typeof(float)) ? 1 : 0;
                        else if (CommandArgs.Count == 1)
                            Register = (ParseCommandArg(CommandArgs[0]).GetType() == typeof(float)) ? 1 : 0;
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[1])] = (ParseCommandArg(CommandArgs[0]).GetType() == typeof(float)) ? 1 : 0;
                        break;

                    case "TYPSTR":
                        // Outputs 1 if the input is of type string, otherwise outputs 0
                        // TYPSTR (void)
                        // TYPSTR (object input)
                        // TYPSTR (object input, int addr)
                        if (CommandArgs.Count == 0)
                            Register = (Register.GetType() == typeof(string)) ? 1 : 0;
                        else if (CommandArgs.Count == 1)
                            Register = (ParseCommandArg(CommandArgs[0]).GetType() == typeof(string)) ? 1 : 0;
                        else
                            Memory[(int)ParseCommandArg(CommandArgs[1])] = (ParseCommandArg(CommandArgs[0]).GetType() == typeof(string)) ? 1 : 0;
                        break;

                    case "IF":
                        // Continue to the next line if the input is true, otherwise jumps to the next ENDIF
                        // Does not jump is no ENDIF command is available later in the file.
                        // IF (void)
                        // IF (object input)
                        if (CommandArgs.Count == 0 && IsArgTrue(Register))
                            break;
                        else if (CommandArgs.Count >= 1 && IsArgTrue(ParseCommandArg(CommandArgs[0])))
                            break;
                        else
                        {
                            for (int i = CurrentLine; i < NumLines; i++)
                            {
                                if (TryReadSbatLine(ProgramLines[i], out string c, out List<object> a) && c.ToUpperInvariant() == "ENDIF")
                                {
                                    CurrentLine = i;
                                    break;
                                }
                            }
                        }
                        break;

                    case "GOTO":
                        // Jumps to the first POS command in the file with a matching name.
                        // Does not jump if the register or argument does not match that of any POS command.
                        // GOTO (void)
                        // GOTO (string pos)
                        if (CommandArgs.Count == 0)
                            left = Register;
                        else
                            left = ParseCommandArg(CommandArgs[0], true);
                        for (int i = 0; i < NumLines; i++)
                        {
                            if (TryReadSbatLine(ProgramLines[i], out string c, out List<object> a)
                                && c.ToUpperInvariant() == "POS"
                                && a.Count >= 1
                                && left.ToString() == ParseCommandArg(a[0], true).ToString())
                            {
                                CurrentLine = i;
                                break;
                            }
                        }
                        break;

                    case "POS":
                    case "ENDIF":
                        break;

                    case "EXE":
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        ExecuteCommandInfo.Invoke(DevConsoleComponent, new object[] { ParseCommandArg(CommandArgs[0], true) });
                        break;

                    case "LOG":
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        Debug.Log(ParseCommandArg(CommandArgs[0], true));
                        break;

                    case "LOGW":
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        Debug.LogWarning(ParseCommandArg(CommandArgs[0], true));
                        break;

                    case "LOGE":
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        Debug.LogError(ParseCommandArg(CommandArgs[0], true));
                        break;

                    case "ECHO":
                        if (CommandArgs.Count == 0)
                            SbatArgsCountException(Command, CommandArgs.Count);
                        Echo = IsArgTrue(ParseCommandArg(CommandArgs[0]));
                        break;

                    case "CLS":
                        ExecuteCommandInfo.Invoke(DevConsoleComponent, new object[] { "ClearLog" });
                        break;

                    case "QUIT":
                        CurrentLine = NumLines;
                        break;

                    case "REM":
                        break;

                    default:
                        if (!string.IsNullOrEmpty(Command))
                            Debug.LogError($"\"{Command}\" is not a recognized Simple Batch Command.");
                        break;
                }

                if (Echo)
                {
                    StringBuilder echoBuilder = new StringBuilder(Command);
                    for (int a = 0; a < CommandArgs.Count; a++)
                    {
                        echoBuilder.Append(a == 0 ? " " : ", ");
                        if (CommandArgs[a].GetType() == typeof(string) && CommandArgs[a].ToString().Any(char.IsWhiteSpace))
                        {
                            echoBuilder.Append("\"" + CommandArgs[a] + "\"");
                        }
                        else
                        {
                            echoBuilder.Append(CommandArgs[a]);
                        }
                    }
                    Debug.Log(echoBuilder);
                }
            }

            CurrentLine++;
        }

        // END Simple Batch Interpreter
        Debug.LogFormat("Execution of Simple Batch Program \"{0}\" completed in {1} ms", args[0], sw.ElapsedMilliseconds);
    }

    /// <summary>
    /// Returns true on parsing a Simple Batch Program line, otherwise returns false.
    /// </summary>
    private bool TryReadSbatLine(string line, out string command, out List<object> args)
    {
        command = "";
        args = new List<object>();

        line = ApplyEscapeSequences(line, false);

        Match lineMatch = ReadSbatLineRegex.Match(line);
        if (!lineMatch.Success || lineMatch.Captures.Count == 0)
        {
            return false;
        }
        command = lineMatch.Groups[1].Value;

        if (lineMatch.Groups.Count == 1)
        {
            // No args after command
            return true;
        }
        // else

        foreach (Capture capture in lineMatch.Groups[2].Captures)
        {
            args.Add(ParseInputArg(capture.Value));
        }

        return true;
    }

    /// <summary>
    /// Parse a string from the dev console to provide a number of arguments.
    /// </summary>
    /// <param name="argsStringOriginal">The string to parse.</param>
    /// <param name="devConsole">Set to true when handling strings from the dev console.</param>
    /// <param name="numArgs">The number of arguments to detect.</param>
    /// <returns></returns>
    private List<object> GetArgsFromString(string argsStringOriginal, int numArgs)
    {
        // Initialize the lists that args data will be copied into
        List<string> outArgsStrings = new List<string>(numArgs);
        List<object> outArgs = new List<object>(numArgs);
        for (int i = 0; i < numArgs; i++)
        {
            outArgsStrings.Add("");
            outArgs.Add(0);
        }

        // If devConsole: Change form of the args (the user types the former to insert the latter)
        // ##      --> #
        // # or \# --> "
        // Other escape sequences apply regardless of devConsole.
        string argsString = ApplyEscapeSequences(argsStringOriginal, true);

        Match argsMatch = ReadArgsStringRegex.Match(argsString);
        if (!argsMatch.Success || argsMatch.Captures.Count == 0)
        {
            Debug.LogError("No matches in ReadArgsStringRegex");
            return outArgs;
        }
        Group argsGroup = argsMatch.Groups[1];
        for (int i = 0; i < Mathf.Min(argsGroup.Captures.Count, numArgs); i++)
        {
            outArgsStrings[i] = argsGroup.Captures[i].Value;
        }

        outArgs[0] = ParseInputArg(outArgsStrings[0], true);
        for (int i = 1; i < outArgsStrings.Count; i++)
        {
            outArgs[i] = ParseInputArg(outArgsStrings[i]);
        }

        return outArgs;
    }

    /// <summary>
    /// Apply the backslash escape sequences. Also applies hash escapes for dev console arguments, and percent escapes otherwise.
    /// </summary>
    /// <returns></returns>
    private string ApplyEscapeSequences(string inputStr, bool devConsole)
    {
        StringBuilder sb = new StringBuilder(100);
        for (int i = 0; i < inputStr.Length; i++)
        {
            if (inputStr[i] == '\\')
            {
                char afterBackslash = '\0';
                i++;
                if (i >= inputStr.Length)    // end of string
                {
                    sb.Append('\\');
                    break;
                }
                else
                {
                    afterBackslash = inputStr[i];
                }

                // Read the next character to determine the escape sequence
                // https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/strings/#string-escape-sequences
                int UnicodeValue = 0;
                switch (afterBackslash)
                {
                    case '\'':
                    case '\"':
                    case '\\':
                        sb.Append(afterBackslash);
                        break;
                    case '0':
                        sb.Append('\0');
                        break;
                    case 'a':
                        sb.Append('\a');
                        break;
                    case 'b':
                        sb.Append('\b');
                        break;
                    case 'f':
                        sb.Append('\f');
                        break;
                    case 'n':
                        sb.Append('\n');
                        break;
                    case 'r':
                        sb.Append('\r');
                        break;
                    case 't':
                        sb.Append('\t');
                        break;
                    case 'v':
                        sb.Append('\v');
                        break;

                    // Read hex chars (advance i to the first hex char)
                    case 'u':
                        i++;
                        if (i + 3 >= inputStr.Length)
                            continue;
                        else if (int.TryParse(
                            inputStr.Substring(i, 4),
                            System.Globalization.NumberStyles.HexNumber,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out UnicodeValue))
                        {
                            sb.Append(char.ConvertFromUtf32(UnicodeValue));
                        }
                        i += 3;
                        break;
                    case 'U':
                        i++;
                        if (i + 7 >= inputStr.Length)
                            continue;
                        else if (int.TryParse(
                            inputStr.Substring(i, 8),
                            System.Globalization.NumberStyles.HexNumber,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out UnicodeValue))
                        {
                            sb.Append(char.ConvertFromUtf32(UnicodeValue));
                        }
                        i += 7;
                        break;
                    case 'x':
                        i++;
                        int i_readable = 4;
                        while (i_readable >= 1)
                        {
                            if (i + i_readable - 1 >= inputStr.Length)
                                break;
                            else if (int.TryParse(
                            inputStr.Substring(i, i_readable),
                            System.Globalization.NumberStyles.HexNumber,
                            System.Globalization.CultureInfo.InvariantCulture,
                            out UnicodeValue))
                            {
                                sb.Append(char.ConvertFromUtf32(UnicodeValue));
                                break;
                            }
                            else
                            {
                                i_readable--;
                            }
                        }
                        i += i_readable - 1;
                        break;

                    default:
                        if (devConsole && afterBackslash == '#')
                        {
                            // Special for dev console \# --> "
                            sb.Append('\"');
                        }
                        else
                        {
                            // Character after backslash is not an escape sequence
                            // --> discard backslash only
                            sb.Append(afterBackslash);
                        }
                        break;
                }
            }
            else if (devConsole && inputStr[i] == '#')
            {
                // Read the next character to check if it is ##
                i++;
                if (i >= inputStr.Length)    // end of string
                {
                    sb.Append('"');
                    break;
                }
                else if (inputStr[i] == '#')    // two hashes
                {
                    sb.Append('#');
                }
                else    // single hash
                {
                    sb.Append('"');
                    i--;
                }
            }
            else
            {
                sb.Append(inputStr[i]);
            }
        }
        return sb.ToString();
    }

    /// <summary>
    /// Substitutes variables in arguments with their underlying values. Numeric values are unchanged.
    /// </summary>
    private object ParseCommandArg(object input, bool composite = false)
    {
        if (input.GetType() != typeof(string))
        {
            // Do not change values that are not strings. Also ensures input is of type string.
            return input;
        }

        StringBuilder outsb = new StringBuilder();
        StringReader sr = new StringReader(input.ToString());
        char next;

        while (sr.Peek() >= 0)
        {
            next = (char)sr.Read();
            if (next == '%')
            {
                if (sr.Peek() < 0)
                {
                    break;
                }
                else
                {
                    // next is after '%'
                    next = (char)sr.Read();
                }

                if (next == '%')
                {
                    outsb.Append('%');
                    continue;
                }
                else if (char.IsDigit(next))
                {
                    if (!composite)
                    {
                        return Memory[int.Parse(next.ToString())];
                    }
                    else
                    {
                        outsb.Append(Memory[int.Parse(next.ToString())]);
                    }
                }
                else
                {
                    // Search for the next '%' and get the value as the variable name
                    StringBuilder varNameSb = new StringBuilder(next.ToString());
                    string varName = null;

                    while (sr.Peek() >= 0)
                    {
                        next = (char)sr.Read();
                        if (next == '%')
                        {
                            varName = varNameSb.ToString();
                            break;
                        }
                        else
                        {
                            varNameSb.Append(next);
                        }
                    }
                    if (varName == null)
                    {
                        break;
                    }

                    if (!composite)
                    {
                        return GetValueFromVarName(varName);
                    }
                    else
                    {
                        outsb.Append(GetValueFromVarName(varName));
                    }
                }
            }
            else
            {
                outsb.Append(next);
            }
        }

        return outsb.ToString();
    }

    /// <summary>
    /// Gets the underlying value or address of the variable.
    /// </summary>
    private object GetValueFromVarName(string varName)
    {
        int index = -1;

        bool asAddress = varName[0] == '&';
        if (asAddress)
        {
            varName = varName.Substring(1);
        }

        switch (varName)
        {
            case "REGISTER":
                index = 100;
                break;
            case "DATE":
                index = 101;
                break;
            case "TIME":
                index = 102;
                break;
            case "IN_DESIGNER":
                index = 103;
                break;
            case "IN_LEVEL":
                index = 104;
                break;
            case "LEVEL_NAME":
                index = 105;
                break;
            case "MAP_NAME":
                index = 106;
                break;
            default:
                for (int i = 10; i < VarNamesUsed; i++)
                {
                    if (varName == VarNames[i])
                    {
                        index = i;
                        break;
                    }
                }
                break;
        }

        // Failed to find variable
        if (index < 0)
            throw new ArgumentException($"The variable \"{varName}\" is not declared in the Simple Batch Program.");

        if (asAddress)
            return index;
        else
            return Memory[index];
    }

    /// <summary>
    /// Parse a string into a supported data type.
    /// </summary>
    /// <param name="str"></param>
    /// <returns></returns>
    private object ParseInputArg(string str, bool forceString = false)
    {
        int parsedInt;
        float parsedFloat;
        bool parsedBool;

        if (forceString)
        {
            if (string.IsNullOrEmpty(str))
            {
                return "";
            }
            else if (str.Length >= 2 && str[0] == '"' && str[str.Length - 1] == '"')
            {
                return str = str.Substring(1, str.Length - 2);
            }
            else
            {
                return str;
            }
        }
        else if (string.IsNullOrEmpty(str))
        {
            return 0;
        }
        else if (int.TryParse(str, out parsedInt))
        {
            return parsedInt;
        }
        else if (float.TryParse(str, out parsedFloat))
        {
            return parsedFloat;
        }
        else if (bool.TryParse(str, out parsedBool))
        {
            return parsedBool ? 1 : 0;
        }
        else    // string
        {
            if (str.Length >= 2 && str[0] == '"' && str[str.Length - 1] == '"')
            {
                str = str.Substring(1, str.Length - 2);
            }
            return str;
        }
    }

    private bool IsArgTrue(object arg)
    {
        if (arg.GetType() == typeof(int))
        {
            return (int)arg != 0;
        }
        else if (arg.GetType() == typeof(float))
        {
            return (float)arg > 0;
        }
        else if (arg.GetType() == typeof(string))
        {
            return !string.IsNullOrEmpty((string)arg);
        }
        throw new ArgumentException("IsArgTrue cannot be applied to a variable of type " + arg.GetType());
    }

    private float ToFloat(object number)
    {
        if (number.GetType() == typeof(int))
        {
            return (int)number;
        }
        else
        {
            return (float)number;
        }
    }

    /// <summary>
    /// Retrieves dev console information which is needed to access the log.
    /// </summary>
    private void GetDevConsoleInfo()
    {
        foreach (Component c in FindObjectsOfType<Component>())
        {
            if (c.GetType().Name == "DeveloperConsole")
            {
                DevConsoleComponent = c;
                DevConsoleTypeInfo = c.GetType().GetTypeInfo();
                break;
            }
        }

        ExecuteCommandInfo = DevConsoleTypeInfo.GetMethod("ExecuteCommand");

        /*
        LogEntriesFieldInfo = DevConsoleTypeInfo.GetDeclaredField("_logEntries");
        LogEntryTypeInfo = LogEntriesFieldInfo.FieldType.GetGenericArguments()[0].GetTypeInfo();
        FieldInfo[] fields = LogEntryTypeInfo.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
        foreach (FieldInfo f in fields)
        {
            if (f.Name.Contains("LogType"))
                LogTypeFieldInfo = f;
            else if (f.Name.Contains("MessageDetails"))
                LogMessageDetailsInfo = f;
            else if (f.Name.Contains("Message"))
                LogMessageInfo = f;
        }
        LogEntriesList = (List<object>)(IList)LogEntriesFieldInfo.GetValue(DevConsoleComponent);
        */
    }

    /// <summary>
    /// Logs the contents of memory to the dev console.
    /// </summary>
    private void PrintMemory()
    {
        if (VarNamesUsed < 10)
        {
            Debug.LogWarning("No Simple Batch Programs have been started yet.");
            return;
        }

        StringBuilder sb = new StringBuilder();
        sb.Append($"Memory dump of Simple Batch Interpreter:\nRegister: [{Register.GetType()}] {Register}");
        for (int i = 0; i < 100; i++)
        {
            string varName = string.IsNullOrEmpty(VarNames[i]) ? "unnamed" : VarNames[i];
            sb.Append($"\nIndex {i:D2} ({varName}): [{Memory[i].GetType()}] {Memory[i]}");
        }
        Debug.Log(sb);
    }

    private void SbatArgsCountException(string commandName, int providedArgsCount)
    {
        throw new ArgumentException($"Simple Batch function \"{commandName}\" cannot be called with {providedArgsCount} arguments");
    }
}
