# Simple Batch Programming Guide

Reference for the SB programming language.

## Syntax Overview

### Data and Variables

The programming language has three data types - integers, floating-point numbers, and strings. Each variable can hold a value, which will be one of the three data types. A maximum of **100 variables** are supported. In addition, an **I/O register** is available and can be used to store the results of commands.

The first ten variables are populated by user inputs. The name of the SB file can be accessed with `%0`. The remaining arguments can be accessed by typing `%1` to `%9`.

    ADD %1, %2

Up to 90 more variables can be declared in the program. The value of the variable can be entered into the argument fields of any command by surrounding the name of the variable by `%` signs. If a string contains any whitespace, it must be surrounded by quotes.

    VAR 123, MyInt
    VAR 4.56, MyFloat
    VAR abc, MyString
    VAR "Hello, World!", MyStringWithSpaces
    LOG %MyInt%
    LOG %MyFloat%
    LOG %MyString%
    LOG %MyStringWithSpaces%
    
In composite string arguments, variables will be substituted:

    VAR "Nacchan", MyName
    LOG "Hello, %MyName%!"
  
Type `%%` to escape the variable substitution and enter a single `%` into a string. The backslash `\` will escape a greater range of characters as according to [C# specifications](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/strings/#string-escape-sequences).

The following variable names are reserved:

    %REGISTER%
    %DATE%
    %TIME%
    %IN_DESIGNER%
    %IN_LEVEL%
    %LEVEL_NAME%
    %MAP_NAME%

Some commands can write values to variables. This is done by providing a pointer to the variable. Adding the `&` symbol between the leading `%` and the name of the variable will produce a pointer to that variable.

    VAR MyVar
    SUB %1, %2, %&MyVar%

### Commands

A **command** is a programming function. It may be followed by some arguments. The command and its arguments must be in the same line, and each line may contain only one command. There can be any amount of whitespace on the line, as long as strings with spaces are delimited with quotes `"`.

The command can be in uppercase, lowercase, or a mixture of the two.

### Running SB Files

Before running SB files, they must be placed in the folder below. This directory appears after the mod has been activated for the first time.

    C:\Users\%USERNAME%\AppData\LocalLow\Jundroo\SimplePlanes\NACHSAVE\SBAT

Use the `ExecuteSB` dev console command to run SB files. As double quotes cannot be entered into the dev console argument, they must be replaced with the hash `#` symbol like in the `DebugExpression` command. If any spaces are used, the entire input string must be surrounded by quotes.

    ExecuteSB hello,SimplePlanes         // correct
    ExecuteSB hello, SimplePlanes        // wrong
    ExecuteSB "hello, SimplePlanes"      // correct
    
    ExecuteSB hello,#Simple Planes#      // wrong
    ExecuteSB "hello,#Simple Planes#"    // correct
    
A collection of SB files is available [here](/sb).

## List of Commands

### Commands by Type

#### Basic Variable Access

Command | Description
:---: | :---
VAR | Declare a new variable
ST | Set the value of a variable
SET | Alias of ST
STC | Set the value of a variable as a composite string

#### Conversion

#### Numeric

#### Integer Bitwise

#### Logical

#### String Operations

#### Comparison

#### Data Types

#### Branching

#### Dev Console

#### Others

### Commands by Alphabetical Order

#### ADD

#### AND

#### BIN

#### CEIL

#### CHAR

#### CLS

#### DIV

#### ECHO

#### ENDIF

#### EXE

#### FLR

#### GOTO

#### HEX

#### IF

#### IOR

#### INOT

#### IOR

#### ISEQ

#### ISGT

#### ISLT

#### IXOR

#### LOG

#### LOGE

#### LOGw

#### MOD

#### MUL

#### NOT

#### OR

#### PARSE

#### POS

#### QUIT

#### RANDOM

#### REM

#### RND

#### SHL

#### SHR

#### ST

#### STC

#### STR

#### STRGET

#### STRLEN

#### SUB

#### SUBSTR

#### TOINT

#### TYPFLT

#### TYPINT

#### TYPSTR

#### VAR

#### XOR
