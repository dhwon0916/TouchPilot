using System.Text.Json;
using TouchPilot;
using Xunit;
public class SettingsTests
{
 [Fact] public void DefaultsDoNotChangeInputOrEnableStartup() { var p=new Preferences(); Assert.False(p.Enabled);Assert.False(p.Stylus);Assert.False(p.StartWithWindows);Assert.Equal(120,p.Delay); }
 [Fact] public void SettingsRoundTripWithoutLosingRulesOrDisplaySelection() {
  var dir=Path.Combine(Path.GetTempPath(),"TouchPilot-test-"+Guid.NewGuid()); var file=Path.Combine(dir,"settings.json");
  try {var p=new Preferences{Enabled=true,Stylus=true,RestoreFocus=true,Delay=340,OnMouseMove=true,AllDisplays=false,Displays=["display1"],KeepApps="editor.exe",RestoreApps="panel.exe",Modifier="Shift"};p.Save(file);var loaded=Preferences.Load(file);Assert.Equal(JsonSerializer.Serialize(p),JsonSerializer.Serialize(loaded));Assert.False(File.Exists(file+".tmp"));}
  finally {if(Directory.Exists(dir))Directory.Delete(dir,true);}
 }
 [Fact] public void PauseDisablesEngineWithoutLosingSavedPreference(){var p=new Preferences{Enabled=true,Stylus=true};using var json=JsonDocument.Parse(p.EngineConfig(true));Assert.False(json.RootElement.GetProperty("enabled").GetBoolean());Assert.True(p.Enabled);Assert.True(json.RootElement.GetProperty("stylus_mouse_independent").GetBoolean());}
 [Fact] public void EmptyDisplaySelectionStaysEmpty(){var p=new Preferences{AllDisplays=false,Displays=[]};using var json=JsonDocument.Parse(p.EngineConfig(false));Assert.False(json.RootElement.GetProperty("touch_all_displays").GetBoolean());Assert.Equal("",json.RootElement.GetProperty("touch_display_bounds").GetString());}
 [Fact] public void InvalidFileIsNotSilentlyOverwritten(){var dir=Path.Combine(Path.GetTempPath(),"TouchPilot-test-"+Guid.NewGuid());Directory.CreateDirectory(dir);var file=Path.Combine(dir,"settings.json");try{File.WriteAllText(file,"invalid");Assert.Throws<JsonException>(()=>Preferences.Load(file));Assert.Equal("invalid",File.ReadAllText(file));}finally{Directory.Delete(dir,true);}}
}
