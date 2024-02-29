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