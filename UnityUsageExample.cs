using UnityEngine;

public class TileGenerator : MonoBehaviour
{
    [ObserveField] //by default default group is created and this field will be added to this group.
    public float def;

    [ObserveField("cat")]
    public int cat;
    
    [ObserveField("default","lol")]
    public int lol = 1;

    [ObserveField("default","lol","cat")]
    public float testvar = 1;

    [ObserveField("emptyGroup")]
    public float emptyGroupValue; //changing this field wont invoke any method beacuse there is no method marked with emptyGroup.
    
    private FieldUpdater<FieldsObserver> observer;

    //Methods need to be public
    [InvokeOnChange] 
    public void Def() //this method is going to be invoked if "def", "lol" or "testvar" value will change
    {
        Debug.Log("Default"); 
    }
    
    [InvokeOnChange("cat")] //this method is going to be invoked if "cat" or "testVar" value will change
    public void Meow()
    {
        Debug.Log("meow");
    }
    
    [InvokeOnChange("lol")] //this method is going to be invoked if "lol" or "testVar" value will change
    public void Hahaha()
    {
        Debug.Log("haha");
    }
    
    [InvokeOnChange("no-fields-group")] 
    public void Empty(); //this method wont be invoked because none of fields is marked to be in "no-fields-group" group
    
    public void CheckForUpdates()
    {
        tileGeneratorUpdater.CheckForUpdates();
    }
    
    private void Awake()
    {
      observer = new<FieldsObserver>(this);
      InvokeRepeating(nameof(CheckForUpdates), 0.0f, 0.5f); //Checking for update after every 0.5 a second
    }
    
}
