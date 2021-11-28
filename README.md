# FieldsObserver
 Simple Observer class, generally for Unity Scripts, but can be used anywhere.
 
 Allows to mark fields and void methods with no args with included Atributes:\
 -ObserveField(param string[] groupName)\
 -InvokeOnChange(param string[] groupName)\
 
 
If field is mareked with ObserveField atribute with no parameters field will be added to default group, same thing will happen with method.
 
After marking fields and methods with inceluded atributes you need to initialize FiledsObserver class.
Contructor takes 1 parameter and it's an object that contains fields that we want to observe.

Last thing to make it work is to invoke CheckForUpdates mehtod periodicaly.

For e.g in Unity Script you can use MonoBehaviour.InvokeRepeating - in this case CheckForUpdates call need to be wraped in to MethodInside this script.

If you want to use it anywhere else than Unity you can use Timers, Tasks, etc.

Example of usage is in other file.
