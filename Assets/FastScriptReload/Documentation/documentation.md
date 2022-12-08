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

### Reporting Compilation Errors
I've put lots of effort to test various code patterns various codebases and also worked with other developers. Still - it's likely you'll find some instances where code would not compile, it's easiest to:
1) Look at compiler error and compare with generated source code, usually it'll be very obvious why issue is occuring
2) Refactor problematic part (look at limitations as they'll explain how)
3) Let me know via support email and I'll get it fixed

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

## Options
You can access Welcome Screen / Options via 'Window -> Fast Script Reload -> Start Screen' - it contains useful information as well as options.

```
Options can aslo be accessed via 'Edit -> Preferences -> Fast Script Reload'
```

### Auto Hot-Reload
By default tool will pick changes made to any file in playmode. You can add exclusions to that behaviour, more on that later.

You can also manually manage reload, to do so:
1) Un-tick 'Enable auto Hot-Reload for changed files' in Options -> Reload page
2) Click Window -> Fast Script Reload -> Force Reload to trigger
3) or call `FastScriptReloadManager.TriggerReloadForChangedFiles()` method from code

### Managing file exclusions

#### via 'Project' context menu
1) Right click on any *.cs file
2) Click Fast Script Reload
3) Add Hot-Reload Exclusion

*You can remove exclusion from same menu*

#### via Exclusions page
To view all exclusions:
1) Right click on any *.cs file
2) Click Fast Script Reload
3) Click Show Exclusions

#### via class attribute
You can also add `[PreventHotReload]` attribute to a class to prevent hot reload for that class.

### Batch script changes and reload every N seconds
Script will batch all your playmode changes and Hot-Reload them in bulk every 3 seconds - you can change that value from 'Reload' options page.

## Production Build Exclusions
Asset code will be excluded from any builds, if you're also using LiveCodeReload and want to create a build which will support Hot-Reload, 
add `LiveScriptReload_IncludeInBuild_Enabled` Scripting Define Symbol via 'Window -> Fast Script Reload -> Welcome Screen -> Build -> Enable Hot Reload For Build'

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

### Assigning `this` to a field references
Similar as above, this could cause some trouble although 'Enable method calls with 'this' as argument fix' setting will fix most of the issues. 

Especially visible with singletons.
eg.

```
public class MySingleton: MonoBehaviour {
    public static MySingleton Instance;
    
    void Start() {
        Instance = this;
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