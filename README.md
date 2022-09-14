# Simple Batch Programming Guide

Reference for the SB programming language.

## Syntax Overview

### Data and Variables

The programming language has three data types - integers, floating-point numbers, and strings. They can be converted to Boolean values according to the relationship below:

- An integer is `true` if it is non-zero.
- A floating-point number is `true` if it is greater than zero.
- A string is `true` if it is not empty.

Each variable can hold a value, which will be one of the three data types. A maximum of **100 variables** are supported. In addition, an **I/O register** is available and can be used to store the results of commands.

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

The following variable names are reserved. They occupy addresses of 100 to 106.

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

Command | Arguments | Description
:---: | :--- | :---
VAR | `string varName` | Declare a new variable with a value of 0
VAR | `object value, string varName` | Declare a new variable and initialize with a value
SET | `object value` | Set the value of the register
SET | `object value, int addr` | Set the value of a variable
ST |  | Alias of SET
STC | `string str` | Set the value of the register as a composite string
STC | `string str, int addr` | Set the value of a variable as a composite string

#### Conversion

Command | Arguments | Description
:---: | :--- | :---
TOINT |  | Alias of FLR
RND | `float value` | Rounds the value to the nearest integer and writes the result to the register
RND | `float value, int addr` | Rounds the value to the nearest integer and writes the result to the address
FLR | `float value` | Rounds down the value and writes the result to the register
FLR | `float value, int addr` | Rounds down the value and writes the result to the address
CEIL | `float value` | Rounds up the value and writes the result to the register
CEIL | `float value, int addr` | Rounds up the value and writes the result to the address
PARSE | `string str` | Parses the string as a number and writes the result to the register
PARSE | `string str, int addr` | Parses the string as a number and writes the result to the address
CHAR | `int value` | Writes a string with the corresponding character and writes the result to the register
CHAR | `int value, int addr` | Writes a string with the corresponding character and writes the result to the address
STR | `object value` | Converts the number into its string representation and writes the result to the register
STR | `object value, int addr` | Converts the number into its string representation and writes the result to the address
HEX | `int value` | Converts the integer into its hexadecimal representation and writes the result to the register
HEX | `int value, int addr` | Converts the integer into its hexadecimal representation and writes the result to the address
BIN | `int value` | Converts the integer into its binary representation and writes the result to the register
BIN | `int value, int addr` | Converts the integer into its binary representation and writes the result to the address

#### Numeric

Command | Arguments | Description
:---: | :--- | :---
ADD | `int right` | Increments the register by `right`
ADD | `float right` | Increments the register by `right`
ADD | `int left, int right` | Calculates the sum of `left` and `right` and writes it to the register
ADD | `float left, float right` | Calculates the sum of `left` and `right` and writes it to the register
ADD | `int left, int right, int addr` | Calculates the sum of `left` and `right` and writes it to the address
ADD | `float left, float right, int addr` | Calculates the sum of `left` and `right` and writes it to the address
SUB | `int right` | Decrements the register by `right`
SUB | `float right` | Decrements the register by `right`
SUB | `int left, int right` | Calculates the difference between `left` and `right` and writes it to the register
SUB | `float left, float right` | Calculates the difference between `left` and `right` and writes it to the register
SUB | `int left, int right, int addr` | Calculates the difference between `left` and `right` and writes it to the address
SUB | `float left, float right, int addr` | Calculates the difference between `left` and `right` and writes it to the address
MUL | `int right` | Multiplies the register by `right`
MUL | `float right` | Multiplies the register by `right`
MUL | `int left, int right` | Calculates the product of `left` and `right` and writes it to the register
MUL | `float left, float right` | Calculates the product of `left` and `right` and writes it to the register
MUL | `int left, int right, int addr` | Calculates the product of `left` and `right` and writes it to the address
MUL | `float left, float right, int addr` | Calculates the product of `left` and `right` and writes it to the address
DIV | `int right` | Divides the register by `right`
DIV | `float right` | Divides the register by `right`
DIV | `int left, int right` | Calculates the quotient of `left` divided by `right` and writes it to the register
DIV | `float left, float right` | Calculates the quotient of `left` divided by `right` and writes it to the register
DIV | `int left, int right, int addr` | Calculates the quotient of `left` divided by `right` and writes it to the address
DIV | `float left, float right, int addr` | Calculates the quotient of `left` divided by `right` and writes it to the address
MOD | `int right` | Calculates the remainder of the register divided by `right` and writes it to the register
MOD | `float right` | Repeats the value of the register by `right` and writes it to the register
MOD | `int left, int right` | Calculates the remainder of `left` divided by `right` and writes it to the register
MOD | `float left, float right` | Repeats the value of `left` by `right` and writes it to the register
MOD | `int left, int right, int addr` | Calculates the remainder of `left` divided by `right` and writes it to the address
MOD | `float left, float right, int addr` | Repeats the value of `left` by `right` and writes it to the address
RANDOM | (none) | Obtains a random floating-point number between 0 and 1, and writes it to the register
RANDOM | `int addr` | Obtains a random floating-point number between 0 and 1, and writes it to the address
RANDOM | `int minInclusive, int maxExclusive` | Obtains a random integer and writes it to the register
RANDOM | `int minInclusive, int maxExclusive, int addr` | Obtains a random integer and writes it to the address
RANDOM | `float minInclusive, float maxInclusive` | Obtains a random floating-point number and writes it to the register
RANDOM | `float minInclusive, float maxInclusive, int addr` | Obtains a random floating-point number and writes it to the address

#### Integer Bitwise
Command | Arguments | Description
:---: | :--- | :---
SHL | `int amount` | Shift bits of the register left by `amount`
SHL | `int value, int amount` | Shift bits of `value` left by `amount`, and writes it to the register
SHL | `int value, int amount, int addr` | Shift bits of `value` left by `amount`, and writes it to the address
SHR | `int amount` | Shift bits of the register right by `amount`
SHR | `int value, int amount` | Shift bits of `value` right by `amount`, and writes it to the register
SHR | `int value, int amount, int addr` | Shift bits of `value` right by `amount`, and writes it to the address
INOT | (none) | Replace the register with its bitwise complement
INOT | `int value` | Gets the bitwise complement of `value` and writes it to the register
INOT | `int value, int addr` | Gets the bitwise complement of `value` and writes it to the address
IAND | `int right` | Gets the bitwise logical AND of the register and `right`, and writes it to the register
IAND | `int left, int right` | Gets the bitwise logical AND of `left` and `right`, and writes it to the register
IAND | `int left, int right, int addr` | Gets the bitwise logical AND of `left` and `right`, and writes it to the address
IOR | `int right` | Gets the bitwise logical OR of the register and `right`, and writes it to the register
IOR | `int left, int right` | Gets the bitwise logical OR of `left` and `right`, and writes it to the register
IOR | `int left, int right, int addr` | Gets the bitwise logical OR of `left` and `right`, and writes it to the address
IXOR | `int right` | Gets the bitwise logical XOR of the register and `right`, and writes it to the register
IXOR | `int left, int right` | Gets the bitwise logical XOR of `left` and `right`, and writes it to the register
IXOR | `int left, int right, int addr` | Gets the bitwise logical XOR of `left` and `right`, and writes it to the address

#### Logical

Command | Arguments | Description
:---: | :--- | :---
ISTRUE | (none) | If the value of the register corresponds to `true`, replaces it with 1. Otherwise, replaces it with 0.
ISTRUE | `object value` | If `value` corresponds to `true`, writes 1 to the register. Otherwise, writes 0.
ISTRUE | `object value, int addr` | If `value` corresponds to `true`, writes 1 to the address. Otherwise, writes 0.
ISFALSE |  | Alias of NOT
NOT | (none) | Gets the logical NOT of the register and writes it to the register
NOT | `object value` | Gets the logical NOT of `value` and writes it to the register
NOT | `object value, int addr` | Gets the logical NOT of `value` and writes it to the address
AND | `object right` | Gets the logical AND of the register and `right`, and writes it to the register
AND | `object left, object right` | Gets the logical AND of `left` and `right`, and writes it to the register
AND | `object left, object right, int addr` | Gets the logical AND of `left` and `right`, and writes it to the address
OR | `object right` | Gets the logical OR of the register and `right`, and writes it to the register
OR | `object left, object right` | Gets the logical OR of `left` and `right`, and writes it to the register
OR | `object left, object right, int addr` | Gets the logical OR of `left` and `right`, and writes it to the address
XOR | `object right` | Gets the logical XOR of the register and `right`, and writes it to the register
XOR | `object left, object right` | Gets the logical XOR of `left` and `right`, and writes it to the register
XOR | `object left, object right, int addr` | Gets the logical XOR of `left` and `right`, and writes it to the address

#### Strings
Command | Arguments | Description
:---: | :--- | :---
ADD | `string right` | Appends the string in the register with `right`

#### Comparison

#### Data Type Checks

#### Branching

#### Dev Console

#### Others
