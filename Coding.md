# Coding Style C#

## Naming Conventions

* Use CamelCase/PascalCasing

```C#
public class FooBar
{
    public void FooFoo()
    {
        //...
    }
    public void BarBar()
    {
        //...
    }
}
```

* Use **using** directives to shortcut names
* Use noun or noun phrases to name a class

* Types: UpperCase, e.g. MyNewClass
* Fields lowerCase, e.g. simpleField
* Delegate: UpperCase and end with 'Delegate', e.g. ImportantDelegate
* Events: UpperCase
* Constants (public): lowerCase
* Constants (public): UpperCase
* Properties: UpperCase
* Arguments: lowerCase

* Prefix interfaces with the letter I.  Interface names are noun (phrases) or adjectives.
* Use singular names for enums
* Use plural names for flags (enums)

* Avoid: Hungarian notation or any other type identification in identifiers
* Allowed: Abbreviations
    * xn for XName
    * x for XElement
    * uri for Uri
    * ftp, web, xml
    
* Not allowed: usrGrp Allowed: userGroup
* No underscores allowed
* Exception:
```C#
public struct Foo
{
  private int bar;

  public Foo(int _bar)
  {
    bar = _bar;
  } // ctor
} // struct Foo
```

## Layout

### Basic Layout

1. Readonly/Consts
2. Events
2. Sub Types, Delegates
3. Fields
4. Ctor/Dtor
5. Methods
6. Properties

### Use of regions

* Use a region to group functionallity
* A functionallity group as the following style, (the minus goes to column 88)
```C#
 #region -- GroupName/Methods -------------------

  // content

  #endregion
```
* If you use a field, constant, type, ... only within this region follow the basic layout.

* Every type within a namespace must be in a region

* It is also possible to group a *long* method, to hide a algorithm
```C#
 #region -- Comment --

  // content

  #endregion
```

### File Layout

1. Defines
2. usings
3. renaming
4. namespace -> basic layout

* Write all modifiers (also private)

### Atributes

* Single attribute single line e.g, [Flags]
* Multiple attributes multible lines
[
Attribute(),
Attribute(),
Attribute()
]

### Use of partial types

* It is allowed to group functionallity in different files

### Static

* Statics always defined at the end of type (and might be separated with // -- Statics --------------------------------)
* Follow basic layout

### Comments

* Use xml comments e.g. <summary>
* Manly place the comment on separate lines, before the statements
* If a comment is only for one short statement, it is allowed at the end of line
* Insert one space between the comment delimiter (//) and the comment text
* Do not create formatted blocks of asterisks around comments.

* Comment language is english

* Mark todo's with  // todo:
* Mark need to fix with // fix:
* Mark hacks with // hack:

* Mark the closing bracket if the span over more then 10 lines
* Always mark closing methods and types

* type comments start with a line of / to column 80

### Code

* Use the default Code Editor settings (smart indenting, four-character indents)
* Use Tabs not spaces
* Write only one statement per line
* Write only one declaration per line
* Add one blank line between definitions
* Use parentheses to make clauses in an expression apparent
* vertically align (curly) brackets, e.g.
```C#
    new XElement("test",
        new XElement("test",
            new XAttribute("a", "b")
        )
    );
```
* Do start a line with a operator
* If block
  * Short if block
```C#
    if (test)
        foo();
    else
        bar();
```
  * Long if block
```C#
    if (test)
    {
        foo(
            manycode
        );
    }
    else
    {
        bar(
            manycode
        );
    }
```

* Concat string with **+**
* Use Environent.NewLine
* Do not concat variables, use String.Format
* Try to use the “@” prefix for string literals instead of escaped strings
* Use String.Empty
* Use String.IsNullOrEmpty
* Use String.Compare with StringComparision

* Use implicit typing for local variables when the type of the variable is obvious from the right side of the assignment, or when the precise type is not important
* Do not use var when the type is not apparent from the right side of the assignment
* Use var in compination with casts, e.g. var test = (Test)GetTest();
* Use object initializers to simplify object creation
* Use "as", "(cast)" in the right way
* Do only use dynamic, when it not avoidable
* Do not rely on the variable name to specify the type of the variable. It might not be correct (e.g. the prefix)
* "tmp" is allowed within 3 lines
* "i", "j", "k", "l" are loop variables
* "cur" is a foreach variable
* "c" is a predicate variable
* "cur" is a prefix for foreach variables

* avoid unsigned datatypes

* array initialization
```C#
byte[] numbers = {1, 2, 3};
var names = new string[] {
  "Max",
  "Moritz"
};
```

* Use explicit exception handling in methods
    * Use only short scopes
    * avoid exceptions
* Only the ui is allowed to catch all
* Do not create exceptions, prefare build exceptions
* Use descriping error text
* Always set the innerException property on thrown exceptions so the exception chain & call stack are maintained

* Simplify your code by using the C# using statement. If you have a try-finally statement in which the only code in the finally block is a call to the Dispose method, use a using statement instead.
* Do not use Enable/Disable scenarios without using or try-finally
* Do not create Enable/Disable functions with bool's, use counter instead
* Use lock() not Monitor

* If you are defining an event handler that you do not need to remove later, use a lambda expression

* Internationalization (expect, no german or us formats, letters)

* Do not trust the caller

* Use only auto properties for simple classes.
* No protected fields are allowed

* .net framework serves first (do not reimplemnt features they are already exist)

* Use predefined type names (int) instead of system types (Int32) on declarations, e.g. int i;
* Use system types instead of predefined types names on static calls, e.g. Int32.Parse("1");

### Enginierung rules

* Do not make members public
* Define small interfaces
* Define simple interfaces
* Do not define chatty interfaces
* Every external interface needs to be auditted
