[![Release](https://github.com/DuncanMcPherson/vectra-language/actions/workflows/release.yml/badge.svg)](https://github.com/DuncanMcPherson/vectra-language/actions/workflows/release.yml)
![GitHub Release](https://img.shields.io/github/v/release/DuncanMcPherson/vectra-language)
![GitHub Release Date](https://img.shields.io/github/release-date/DuncanMcPherson/vectra-language)

# Vectra Language

Vectra is a statically-typed, object-oriented programming language with a clean, C#-inspired syntax. It is implemented as a tree-walk interpreter written in C# targeting .NET 10.

> **Status:** Work in progress. Package and module loading are functional, and the interpreter is able to correctly select entry points.

---

## Table of Contents

- [Features](#features)
- [Project Structure](#project-structure)
- [Getting Started](#getting-started)
- [Language Reference](#language-reference)
  - [Namespaces and Imports](#namespaces-and-imports)
  - [Types](#types)
  - [Variables](#variables)
  - [Classes](#classes)
  - [Properties](#properties)
  - [Interfaces](#interfaces)
  - [Enums](#enums)
  - [Control Flow](#control-flow)
  - [Operators](#operators)
  - [Built-in Functions](#built-in-functions)
- [Examples](#examples)
- [Architecture](#architecture)
- [Building](#building)
- [Running a Vectra File](#running-a-vectra-file)

---

## Features

- **Statically typed** with type inference via `let`
- **Object-oriented**: classes, interfaces, and enums
- **Properties** with explicit `get` and `set` accessors
- **Enums** with constructor parameters and methods
- **Namespaces** (`space`) and import declarations (`enter`)
- **Optional chaining** with `?.` for null-safe member access
- **Destructuring assignment**: `{a, b} = obj`
- **Lexical scoping** with closure support
- **Built-in I/O**: `PrintLine`, `Print`, `ReadLine`
- **Built-in object methods**: `ToString()`, `GetType()`, `Equals()`, `GetHashCode()`

---

## Project Structure

```
vectra-language/
├── src/
│   ├── VectraLang/                    # CLI entry point
│   │   ├── Program.cs
│   │   └── Formatters/
│   │       └── AstPrinter.cs         # Debug AST pretty-printer
│   ├── VectraLang.Ast/               # Lexer, parser, and AST node definitions
│   │   ├── Lexer.cs
│   │   ├── Parser.cs
│   │   ├── AstNodes/
│   │   │   ├── Node.cs               # Base node types (Expr, Stmt, Node)
│   │   │   ├── Types.cs              # Type nodes
│   │   │   ├── Declarations.cs       # Class, interface, enum, method, field nodes
│   │   │   ├── Statements.cs         # Statement nodes
│   │   │   ├── Expressions.cs        # Expression nodes
│   │   │   └── Literals.cs           # Literal value nodes
│   │   └── Tokens/
│   │       ├── Token.cs
│   │       ├── TokenType.cs
│   │       ├── TokenExtensions.cs
│   │       └── TokenLocation.cs
│   └── VectraLang.Interpreter/       # Tree-walk interpreter
│       ├── Interpreter.cs
│       ├── RuntimeValue.cs           # Runtime value types
│       ├── VectraEnvironment.cs      # Lexical scope management
│       ├── ObjectMethodsRegistry.cs  # Built-in object methods
│       ├── RuntimeException.cs
│       └── ReturnException.cs
├── examples/
│   ├── SingleFileExamples/
│   │   ├── HelloWorldExample.vec
│   │   └── PropertyAccessExample.vec
│   └── EnumTests/
│       └── EnumNoParameters.vec
└── VectraLang.slnx
```

---

## Getting Started

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

### Building

```bash
git clone https://github.com/DuncanMcPherson/vectra-language.git
cd vectra-language
dotnet build
```

### Running a Vectra File

```bash
dotnet run --project src/VectraLang/VectraLang.csproj -- <path-to-file.vec>
```

**Example:**

```bash
dotnet run --project src/VectraLang/VectraLang.csproj -- examples/SingleFileExamples/HelloWorldExample.vec
```

---

## Language Reference

### Namespaces and Imports

Every Vectra file declares a namespace using the `space` keyword and can import other namespaces using `enter`.

```vectra
enter Vectra.Core.Models;

space MyApp;
```

### Types

| Type     | Description              |
|----------|--------------------------|
| `int`    | 32-bit integer           |
| `float`  | Floating point number    |
| `string` | UTF-16 string            |
| `bool`   | Boolean (`true`/`false`) |
| `void`   | No return value          |

### Variables

Variables can be declared with an explicit type or inferred with `let`:

```vectra
int x = 10;
float pi = 3.14;
string name = "Vectra";
bool active = true;

let count = 42;       // type inferred as int
let message = "hi";   // type inferred as string
```

### Classes

Classes support fields, properties, constructors, and methods. Visibility modifiers (`public`, `private`, `protected`) and `static` are supported.

```vectra
public class Animal {
    private string _name;

    public string Name {
        get { return this._name; }
        set { this._name = value; }
    }

    public Animal(string name) {
        this.Name = name;
    }

    public string Speak() {
        return "...";
    }
}
```

Creating an instance:

```vectra
let a = new Animal("Dog");
PrintLine(a.Name);
```

### Properties

Properties are declared with optional `get` and `set` blocks:

```vectra
public class Person {
    private int _age;

    public int Age {
        get { return this._age; }
        set { this._age = value; }
    }
}
```

### Interfaces

Interfaces declare method signatures that classes can implement:

```vectra
public interface IShape {
    float Area();
    float Perimeter();
}
```

### Enums

Enums support variants with optional constructor parameters and static methods:

```vectra
enum Direction {
    North();
    South();
    East();
    West();

    public static int Count() {
        return 4;
    }
}
```

Enums with parameters:

```vectra
enum Status {
    Active(1);
    Inactive(0);
    Pending(2);

    Status(int code) {}

    public static Status FromCode(int code) {
        if (code == 1) { return Status.Active; }
        if (code == 0) { return Status.Inactive; }
        return Status.Pending;
    }
}
```

Access enum variants with dot notation:

```vectra
let dir = Direction.North;
```

### Control Flow

#### If / Else

```vectra
if (x > 0) {
    PrintLine("positive");
} else if (x < 0) {
    PrintLine("negative");
} else {
    PrintLine("zero");
}
```

#### While

```vectra
while (x > 0) {
    x = x - 1;
}
```

#### For

```vectra
for (int i = 0; i < 10; i = i + 1) {
    PrintLine(i.ToString());
}
```

#### Break and Continue

```vectra
while (true) {
    if (done) { break; }
    if (skip) { continue; }
}
```

#### Return

```vectra
public int Add(int a, int b) {
    return a + b;
}
```

### Operators

| Category    | Operators                              |
|-------------|----------------------------------------|
| Arithmetic  | `+`, `-`, `*`, `/`, `%`               |
| Comparison  | `==`, `!=`, `<`, `<=`, `>`, `>=`      |
| Logical     | `&&`, `\|\|`, `!`                      |
| Assignment  | `=`                                    |
| Member      | `.` (access), `?.` (optional chaining) |

### Optional Chaining

Use `?.` to safely access a member that may be `null`:

```vectra
let name = person?.Name;
```

### Destructuring

Destructure an object's fields into local variables:

```vectra
{name, age} = person;
```

### Built-in Functions

| Function          | Description                           |
|-------------------|---------------------------------------|
| `PrintLine(value)` | Prints a value followed by a newline |
| `Print(value)`    | Prints a value without a newline      |
| `ReadLine()`      | Reads a line from standard input      |

### Built-in Object Methods

Every value exposes the following methods:

| Method          | Description                                  |
|-----------------|----------------------------------------------|
| `ToString()`    | Returns a string representation of the value |
| `GetType()`     | Returns the type name as a string            |
| `Equals(other)` | Checks value equality                        |
| `GetHashCode()` | Returns the hash code                        |
| `GetFullName()` | Returns the fully-qualified type name        |

---

## Examples

### Hello World

```vectra
enter Vectra.Core.Models;

space HelloWorldExample;

public class Program {
    public void Main() {
        PrintLine("Hello World!");
    }
}
```

### Properties and Object Methods

```vectra
space PropertyAccessExample;

public static class Program {
    public static void Main() {
        let a = 5;
        PrintLine(a.ToString());
        PrintLine(a.GetType());

        let p = new Person("Duncan", 29);
        PrintLine(p.Name);
        p.Age = 30;
    }
}

public class Person {
    private string _name;

    string Name {
        get { return this._name; }
        set { this._name = value; }
    }

    private int _age;

    public int Age {
        get { return this._age; }
        set { this._age = value; }
    }

    public Person(string name, int age) {
        this.Name = name;
        this.Age = age;
    }
}
```

### Enums

```vectra
space Enums;

enum Color {
    Red();
    Green();
    Blue();

    public static int Count() {
        return 3;
    }
}

enum Priority {
    Low(1);
    Medium(5);
    High(10);

    Priority(int level) {}

    public static Priority Next(Priority p) {
        if (p == Priority.Low) { return Priority.Medium; }
        if (p == Priority.Medium) { return Priority.High; }
        return Priority.Low;
    }
}
```

---

## Architecture

Vectra is implemented as a classic three-phase pipeline:

```
Source Text
    │
    ▼
┌─────────┐
│  Lexer  │  Converts source text into a flat list of tokens
└─────────┘
    │
    ▼
┌─────────┐
│ Parser  │  Builds an Abstract Syntax Tree (AST) using recursive descent
└─────────┘
    │
    ▼
┌─────────────┐
│ Interpreter │  Walks the AST and executes the program
└─────────────┘
```

### Key Design Decisions

- **AST nodes** are implemented as C# `record` types for immutability and structural equality.
- **Lexical scoping** is implemented with a `VectraEnvironment` chain, where each scope holds a reference to its enclosing scope.
- **Control flow** (return, break, continue) is implemented using C# exceptions, which unwind the call stack cleanly.
- **Type inference** via `let` is resolved at runtime; static type checking is deferred.
- **Generic types** are parsed but not yet fully implemented.

---

## License

This project is currently unlicensed. See the repository for the latest information.
