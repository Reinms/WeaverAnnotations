# WeaverAnnotations
Tool for applying IL patches as a postbuild event.

## General setup
- Build from source (releases eventually)
- Add the build event in either the properties window or the .csproj file
### Properties window
- Select build events on sidebar
- In the post build section, paste the following `"[[[PathToWeaverAnnotationsSolutionDirectory]]]\__BUILD\netcoreapp3.1\WeaverAnnotations.Patcher.exe" "$(TargetPath)"`
- Make sure to replace the `[[[PathToWeaverAnnotationsSolutionDirectory]]]` with the proper path
- Save
### .csproj file
- Open the .csproj file
- Add the following:
```xml
  <Target Name="PostBuild" AfterTargets="PostBuildEvent">
    <Exec Command="&quot;[[[PathToWeaverAnnotationsSolutionDirectory]]]\__BUILD\netcoreapp3.1\WeaverAnnotations.Patcher.exe&quot; &quot;$(TargetPath)&quot;" />
  </Target>
```
- Make sure to replace the `[[[PathToWeaverAnnotationsSolutionDirectory]]]` with the proper path
- Save

## Usage
- In your project, add a reference to `WeaverAnnotations.Attributes.dll` which can be found in the same location as the patcher exe file.
- Add a patcher attribute to your assembly
```cs
using WeaverAnnotations.Attributes;
[assembly: InlineProperties]
```
- Build

## Extending
- You can extend this with custom patches by providing two assemblies.
- The first assembly should have no dependencies other than `WeaverAnnotations.Attributes.dll`, and is what you add to the projects you wish to patch as a reference.
- Inside it you will simply define an attribute type similar to the following
```cs
namespace myNamespace.Attributes
{
  using WeaverAnnotations.Attributes;
  
  public class PromoteToModuleAttribute : BaseAttribute { }
}
```
- In the second assembly you must reference `WeaverAnnotations.Core.dll`, `WeaverAnnotations.Attributes.dll`, and your first assembly.
- Inside it you will define a type inheriting `WeaverAnnotations.Core.PatcherType.Patch<YourAttributeType>`
- You must add a `PatcherAttributeMap` assembly attribute to the second assembly that defines the mapping from your custom attribute to the patcher.
- Inside your patch type, you should override the passes property to add a custom type that inherits `WeaverAnnotations.Core.PatcherType.Pass<YourAttributeType>`
- You can provide any number of pass types, and they will be run in sequence. For more complex sequences of passes, you can leverage a custom IEnumerable type.
- A patch also can override a `Prepare()` and a `Finish()` method to define code that should run at the start and end of that patch.
- A pass should implement some combination of the `IAssemblyPass`, `IModulePass`, `ITypePass`, `IPropertyPass`, `IEventPass`, `IFieldPass`, `IMethodPass`, `IPreparePass`, and `IFinishPass` interfaces.
- The order these run in is as follows:
```
IPreparePass
IAssemblyPass
IModulePass
ITypePass (On top level types)
Immediately after any ITypePass runs, passes are run on members of that type in the following order
  IPropertyPass
  IEventPass
  IFieldPass
  IMethodPass
  ITypePass (For nested types, which will then follow this same order for their members recursively)
IFinishPass
```
- After your patch is written, you can add it to the patcher in one of two ways.
- First, by copying the two assemblies to the `__BUILD/netcoreapp3.1/Extensions` folder
- Or, by adding the path to the directories of those two assemblies as command line arguments to the end of the build event. You can supply as many of these as you like. Note that the patcher will also search through all subdirectories.

