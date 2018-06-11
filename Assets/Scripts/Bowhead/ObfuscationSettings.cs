// Copyright (c) 2018 Pocketwatch Games LLC.

using System.Reflection;
#if SHIP
[assembly: ObfuscateAssembly(true)]
// Enum's are loaded in FlowCanvas by name and value.
[assembly: Obfuscation(Feature = "Apply to type * when enum: renaming", Exclude = true, ApplyToMembers = true)]
// OnNetMsg is bound dynamically via reflection.
[assembly: Obfuscation(Feature = "Apply to type World when class: apply to member OnNetMsg when method: renaming", Exclude = true)]
[assembly: Obfuscation(Feature = "Apply to type * when class and inherits('World'): apply to member OnNetMsg when method: renaming", Exclude = true)]
// NetMsgs are bound via hash code generated from their name.
[assembly: Obfuscation(Feature = "Apply to type * when class and inherits('NetMsg') and not abstract: renaming", Exclude = true, ApplyToMembers = false)]
// classIDs for SerializableObjects are generated via names and may be created from common abstract base classes.
[assembly: Obfuscation(Feature = "Apply to type * when class and inherits('SerializableObject'): renaming", Exclude = true, ApplyToMembers = false)]
// Unit actions are bound via GetType()
[assembly: Obfuscation(Feature = "Apply to type * when class and inherits('Bowhead.Actors.UnitActionBase') and not abstract: renaming", Exclude = true, ApplyToMembers = false)]
// Spells are bound via GetType()
[assembly: Obfuscation(Feature = "Apply to type * when class and inherits('Bowhead.Actors.Spells.Spell') and not abstract: renaming", Exclude = true, ApplyToMembers = false)]
// FlowCanvas doesn't work when renamed
[assembly: Obfuscation(Feature = "Apply to type FlowCanvas.*: renaming", Exclude = true)]
[assembly: Obfuscation(Feature = "Apply to type FlowCanvas.*: apply to member *: renaming", Exclude = true)]
[assembly: Obfuscation(Feature = "Apply to type FlowCanvas.*: apply to member *: arguments renaming", Exclude = true)]
[assembly: Obfuscation(Feature = "Apply to type NodeCanvas.*: renaming", Exclude = true)]
[assembly: Obfuscation(Feature = "Apply to type NodeCanvas.*: apply to member *: renaming", Exclude = true)]
[assembly: Obfuscation(Feature = "Apply to type NodeCanvas.*: apply to member *: arguments renaming", Exclude = true)]
[assembly: Obfuscation(Feature = "Apply to type * when class and inherits('FlowCanvas.Nodes.SimplexNode'): renaming", Exclude = true)]
[assembly: Obfuscation(Feature = "Apply to type * when class and inherits('NodeCanvas.Framework.Node'): renaming", Exclude = true)]
// Bowhead scripting nodes for FlowCanvas shouldn't be renamed either since FlowCanvas looks for them by name.
[assembly: Obfuscation(Feature = "Apply to type Bowhead.Scripting.*: renaming", Exclude = true, ApplyToMembers = false)]
[assembly: Obfuscation(Feature = "Apply to type Bowhead.Scripting.*: apply to member * when public or virtual: renaming", Exclude = true)]
[assembly: Obfuscation(Feature = "Apply to type Bowhead.Scripting.*: apply to member * when public or virtual: arguments renaming", Exclude = true)]
// CFuncs should maintain their readable names and parameters for console.
[assembly: Obfuscation(Feature = "Apply to type *: apply to member * when has_attribute('CFunc'): renaming", Exclude = true)]
[assembly: Obfuscation(Feature = "Apply to type *: apply to member * when has_attribute('CFunc'): arguments renaming", Exclude = true)]
// OnRep_ fields in SerializableObjects should not be renamed since they are bound via reflection for replication events.
[assembly: Obfuscation(Feature = "Apply to type * when class and inherits('SerializableObject'): apply to member OnRep_* : renaming", Exclude = true)]
// GameModes are bound via GetType()
[assembly: Obfuscation(Feature = "Apply to type * when class and inherits('Bowhead.Server.GameMode') and not abstract: renaming", Exclude = true, ApplyToMembers = false)]
// Don't rename fields that Unity serializes.
[assembly: Obfuscation(Feature = "Apply to type * when has_attribute('System.SerializableAttribute'): renaming", Exclude = true, ApplyToMembers = false)]
[assembly: Obfuscation(Feature = "Apply to type * when has_attribute('System.SerializableAttribute'): apply to member * when public and field and not (static or const): renaming", Exclude = true)]
[assembly: Obfuscation(Feature = "Apply to type * when has_attribute('System.SerializableAttribute'): apply to member * when has_attribute('UnityEngine.SerializeField'): renaming", Exclude = true)]
[assembly: Obfuscation(Feature = "Apply to type * when class and inherits('UnityEngine.ScriptableObject'): apply to member * when has_attribute('UnityEngine.SerializeField'): renaming", Exclude = true)]
[assembly: Obfuscation(Feature = "Apply to type * when class and inherits('UnityEngine.ScriptableObject'): renaming", Exclude = true, ApplyToMembers = false)]
[assembly: Obfuscation(Feature = "Apply to type * when class and inherits('UnityEngine.ScriptableObject'): apply to member * when public and field and not (static or const): renaming", Exclude = true)]
[assembly: Obfuscation(Feature = "Apply to type * when class and inherits('UnityEngine.MonoBehaviour'): apply to member * when method and public: renaming", Exclude = true)]
// CodeStage FPS counter
[assembly: Obfuscation(Feature = "Apply to type CodeStage.*: renaming", Exclude = true)]
// By default rename ALL symbols public or not
[assembly: Obfuscation(Feature = "Apply to type *: forced rename", Exclude = false)]
[assembly: Obfuscation(Feature = "encrypt symbol names with password {KaZ6OrutO/kxfLsp>lnrtwbIwu6zp38v[CSCvRvtJsFA>aoICRkv8XhUhCxbXPo", Exclude = false)]
[assembly: Obfuscation(Feature = "toolset minimal version 4.0")]
#else
// turn off all obfuscation
[assembly: Obfuscation(Feature = "Apply to type *: all", Exclude = true, ApplyToMembers = true)]
#endif
