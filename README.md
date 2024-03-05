# TwiLua 🦄🌒

A pure C# implementation of the Lua runtime.

* Targets **.NET Standard 2.0** with **zero dependencies**.
* Fully **AOT Compatible** - even with reflection disabled.
* **No GC allocations** for math or C# interop.

## Why?

There are at least 3 other libraries that do, broadly, the same thing that TwiLua does:

* [**MoonSharp**](https://github.com/moonsharp-devs/moonsharp), my direct inspiration. It's good, and it works well, but it's a little too allocation-heavy for my liking. I wanted a codebase that was simpler and more performant. It seems to have fallen out of support, but there's a dedicated userbase, and various forks exist (though none yet seem to be the 'official' new home for the project).
* [**KeraLua**](https://github.com/NLua/KeraLua), a thin C# wrapper around the native Lua library. The API can be a little tricky, and the native dependency obviously limits where you can use it, but it's a great choice for performance-critical use cases. It's the back-end of [**NLua**](https://github.com/NLua/NLua) which provides an idiomatic API and easy C# interop.
* [**KopiLua**](https://github.com/NLua/KopiLua), an almost line-by-line port of the Lua codebase to C#. Could be used as an NLua backend at one time, though it's considered legacy by this point and quite old.

Versus MoonSharp, TwiLua aims to be **faster** and **lighter**.

Versus KeraLua/NLua, TwiLua aims to be **more portable**.

## Goals

* **Performance** - We should be faster than MoonSharp and KopiLua in most cases, otherwise what's the point ;)
* **Minimal allocations** - To be suitable for game-dev, we can't allocate megabytes of data during execution.
* **Ease of use** - The main API should be idiomatic and straightforward.
* **Modularity** - Where possible, features and dependencies, such as reflection-based interop, should be opt-in.
* **Compatibility** - The vast majority of existing Lua 5.4 code should run as intended.

## Non-goals

* **Zero allocations** - This isn't really possible without some severe compromises in other areas.
* **Absolute compatibility** - Certain edge cases and rarely-used features simply aren't worth the trouble of implementing, like week-year formatting and weird POSIX-specific locale rules.

## Getting started

If using **Unity**, follow the instructions [here](https://docs.unity3d.com/Manual/upm-ui-giturl.html) to install a package from Git. Use the following URL:

```
https://github.com/hamish-milne/TwiLua.git?path=/TwiLua
```

Otherwise, simply run:

```
# doesn't work yet
# dotnet add package TwiLua
```



```csharp
using TwiLua;

var lua = new Lua().LoadLibs();

lua.DoString(@"
Console = import('System')

Console.WriteLine('The Lua version is: ' .. VERSION)
");

lua.Globals["theAnswer"] = 42;
Console.WriteLine($"The meaning of life is ${lua.DoString("return theAnswer")[0]}");
```

## Incompatibilities by design

* There is no attempt to be compatible with the Lua C API. We are only concerned about compatibility with existing Lua code.
* We use `string` objects to represent Lua strings, rather than byte arrays.
    * `string.byte` will return values greater than 255 for non-ASCII characters
    * `string.char` will accept values up to 65535
    * `string.reverse`, `string.sub`, `string.len` and the length operator work in UTF-16 code units
    * The `utf8` library is not implemented. This library was conceived at a time when manipulating code points was considered important, but [this isn't really the case](https://utf8everywhere.org/#myth.strlen).
* Lua's IO library treats strings as binary data. There are a few ways to account for this, but in our case we use different encodings when converting between byte arrays and strings:
    * **UTF8** for files opened in text mode (i.e. when `b` is not specified) and the `c`, `z`, and `s` formats for `string.pack`/`string.unpack`. This ensures that non-English text will interoperate correctly with the .NET string APIs, though it will mangle non-text data.
    * **Latin1** (aka ISO 8859-1, Windows-28591) where each `byte` is simply padded with zeroes to make a `char`, for files opened in binary mode as well as the packed data in `string.pack`/`string.unpack`. This ensures that arbitrary binary data can be round-tripped without errors, and string operations will work in terms of bytes. However non-English text will be mangled.
    * The stream's native encoding where one already exists (i.e. the Console)
* Lua uses 'native sizes' for certain operations - mainly `string.pack`. TwiLua makes the following assumptions:
    * `lua_Number` is `double` (64-bit IEEE-754 floating point)
    * Native `long` is `int` (32-bit integer)
    * Native `short` is `short` (16-bit integer)
    * `lua_Integer` is `long` (64-bit integer)
    * `size_t` and `lua_Unsigned` are `ulong`
    * 'Native size' and 'Native alignment' are 4 bytes
    * 'Native endinanness' is little-endian
* All numbers are stored as `double`. There are no separate 'float' and 'integer' data types. A number `x` is considered an integer where `x % 1 == 0`. Since Lua will coerce floats to integers where required, most programs are unaffected.
    * `math.type(1.0)` returns `integer` in TwiLua, but `float` in Lua.
    * `math.maxinteger` and `math.mininteger` return `2^53` and `-2^53` respectively.
    * There is no special integer arithmetic. `math.maxinteger + 1` will not 'wrap around' but will instead lose precision.
    * It follows that all numbers with a magnitude greater than `2^53` are considered integers.
    * Very large 64-bit numbers, such as pointers, cannot be represented as a Number.
* TwiLua uses the host runtime's garbage collector, so `collectgarbage` has no effect, apart from `collectgarbage('collect')` which will call `GC.Collect()`.
