# Fast Script Reload

Tool will allow you to iterate quicker on your code. You simply go into play mode, make a change to any file and it'll be compiled on the fly and hot-reloaded in your running play-mode session.

## Getting started
1) Import (welcome screen will introduce you to options / etc)
2) Open example scene `FastScriptReload/Examples/Scenes/ExampleScene`
3) Play
4) Make code change, eg to `FunctionLibrary` (in `Assets/FastScriptReload/Examples/Scripts/`), Change `Ripple` method (eg change line before return statement to `p.z = v * 10`
5) See results

```
Example scene 'Point' material should automatically detect URP or surface shader, if it shows pink, please adjust by picking shader manually:
1) URP: 'Shader Graphs/Point URP'
2) Surface: 'Graph/Point Surface'
```

## Executing custom code on hot reload
Custom code can be executed on hot reload by adding a method to changed script.

**You can see example by adjusting code in 'Graph.cs' file.**

```
    void OnScriptHotReload()
    {
        //do whatever you want to do with access to instance via 'this'
    }
```

```
    static void OnScriptHotReloadNoInstance()
    {
       //do whatever you want to do without instance
       //useful if you've added brand new type
       // or want to simply execute some code without |any instance created.
       //Like reload scene, call test function etc
    }
```

## Running outside of editor workflow

It's a development tool, by default no runtime scripts will be included outside of Editor.

**If you want test standalone / Android builds in same manner please look at extension tool 'Live Script Reload'**

## Performance

Your app performance won't be affected in any meaningful way.
Biggest bit is additional memory used for your re-compiled code.
Won't be visuble unless you make 100s of changes in same play-session.

## Limitations
There are some limitation due to the approach taken bu the tool to hot-reload your scripts.

### Breakpoints in hot-reloaded scripts won't be hit, sorry!

- only for the scripts you changed, others will work
- with how quick it compiles and reloads you may not even need a debugger

### Passing `this` reference to method that expect concrete class implementation

It'll throw compilation error `The best overloaded method match for xxx has some invalid arguments` - this is due to the fact that changed code is technically different type.
The code will need to be adjusted to depend on some abstraction instead (before hot-reload).

`**By default experimental setting 'Enable method calls with 'this' as argument fix' is turned on in options, and should fix 'this' calls issue.
If you see issues with that please turn setting off and get in touch via support email.**

This code would cause the above error.
```
public class EnemyController: MonoBehaviour { 
    EnemyManager m_EnemyManager;

    void Start()
    {
        //calling 'this' causes issues as after hot-reload the type of EnemyController will change to 'EnemyController__Patched_'
        m_EnemyManager.RegisterEnemy(this);
    }
}

public class EnemyManager : MonoBehaviour {
    public void RegisterEnemy(EnemyController enemy) { //RegisterEnemy method expects parameter of concrete type (EnemyController) 
        //impementation
    }
}
```

It could be changed to support Hot-Reload in following way:

1) Don't depend on concrete implementations, instead use interfaces/abstraction
```
public class EnemyController: MonoBehaviour, IRegistrableEnemy { 
    EnemyManager m_EnemyManager;

    void Start()
    {
        //calling this causes issues as after hot-reload the type of EnemyController will change
        m_EnemyManager.RegisterEnemy(this);
    }
}

public class EnemyManager : MonoBehaviour {
    public void RegisterEnemy(IRegistrableEnemy enemy) { //Using interface will go around error
        //impementation
    }
}

public interface IRegistrableEnemy
{
    //implementation
}
```

2) Adjust method param to have common base class
```
public class EnemyManager : MonoBehaviour {
    public void RegisterEnemy(MonoBehaviour enemy) { //Using common MonoBehaviour will go around error
        //impementation
    }
}
```

### Creating new public methods
Hot-reload for new methods will only work with private methods (only called by changed code)

### Adding new fields
As with methods, adding new fields is not supported in play mode.
You can however simply create local variable and later quickly refactor that out.

eg. for a simple class that moves position by some vector on every update

*Initial class before play mode entered*
```
public class SimpleTransformMover: MonoBehaviour {
   void Update() {
        transform.position += new Vector3(1, 0, 0);
    }
}
```

*Changes in playmode*
```
public class SimpleTransformMover: MonoBehaviour {
   //public Vector3 _moveBy = new Vector3(1, 0, 0); //1) do not introduce fields in play mode
    
   void Update() {
        var _moveBy = new Vector3(1, 0, 0); //2) instead declare variable in method scope 
        // (optionally with instance scope name-convention)
   
        // transform.position += new Vector3(1, 0, 0); //original code - now will use variable
        transform.position += _moveBy; //3) changed code - uses local variable
        
        4) iterate as needed and after play mode simply refactor added variables as fields
    }
}
```

**WARNING**

Tool will compile and hot-reload newly added fileds but it'll likely result in unexpected behaviour. eg.
```
	public class SomeClass : MonoBehaviour {
        [SerializeField] private int _val = 1; //added after initial compilation
	    
	    void Update() {
	        Debug.Log($"val: {_val}"); //added after initial compilation
	    }
	}
```

### No IL2CPP support
Asset runs based on specific .NET functionality, IL2CPP builds will not be supported. Although as this is development workflow aid you can build your APK with Mono backend (android) and change later.

### Windows only
Tool is unlikely to run outside of windows os.

### Adding new references
When you're trying to reference new code in play-mode session that'll fail if assembly is not yet referencing that (most often happens when using AsmDefs that are not yet referencing each other)

## Roadmap
- add debugger support for hot-reloaded scripts