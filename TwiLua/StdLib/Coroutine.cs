using System;

namespace TwiLua.StdLib
{

    public static class Coroutine
    {

        public static void Load(LuaTable globals)
        {
            globals["coroutine"] = new LuaTable {
                {"create", s => {
                    if (s.Count < 1) {
                        throw new WrongNumberOfArguments();
                    }
                    var func = s[1].Function;
                    if (func == null) {
                        throw new LuaRuntimeError("Argument to 'coroutine.create' must be a function");
                    }
                    var thread = new LuaThread(isMain: false);
                    thread.SetMainFunction(func);
                    thread.IsYieldable = true;
                    return s.Return(thread);
                }},
                {"resume", s => {
                    if (s.Count < 1) {
                        throw new WrongNumberOfArguments();
                    }
                    var thread = s[1].Thread;
                    if (thread == null) {
                        throw new LuaRuntimeError("Argument to 'coroutine.resume' must be a thread");
                    }
                    if (thread.IsDead) {
                        throw new LuaRuntimeError("Cannot resume dead thread");
                    }
                    for (int i = 2; i <= s.Count; i++) {
                        thread.Push(s[i]);
                    }
                    thread.IsYieldable = true;
                    s[0] = thread.Resume();
                    for (int i = 0; i < thread.Count; i++) {
                        s[i + 1] = thread[i];
                    }
                    return thread.Count + 1;
                }},
                {"running", s => {
                    s[0] = s;
                    s[1] = s.IsMain;
                    return 2;
                }},
                {"status", s => {
                    if (s.Count < 1) {
                        throw new WrongNumberOfArguments();
                    }
                    var thread = s[1].Thread;
                    if (thread == null) {
                        throw new LuaRuntimeError("Argument to 'coroutine.status' must be a thread");
                    }
                    if (thread == s) {
                        return s.Return("running");
                    }
                    if (thread.IsRunning) {
                        return s.Return("normal");
                    }
                    if (thread.IsDead) {
                        return s.Return("dead");
                    }
                    return s.Return("suspended");
                }},
                {"yield", s => {
                    s.Yield();
                    return 0;
                }},
                {"isyieldable", s => {
                    var co = s;
                    if (s.Count > 0) {
                        var thread = s[1].Thread;
                        if (thread == null) {
                            throw new LuaRuntimeError("Argument to 'coroutine.isyieldable' must be a thread");
                        }
                        co = thread;
                    }
                    return s.Return(co.IsYieldable);
                }}
            };
        }
    }
}