using System.Drawing;
using System.Windows.Forms;
using TouchPilot;
using Xunit;
public class WindowTests
{
 [Fact] public void SettingsWindowRendersAndDisposesOnStaThread()
 {
  Exception? failure=null;
  var thread=new Thread(()=>{try{
   Application.EnableVisualStyles();
   Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
   using var window=new SettingsWindow(false);
   var controls=Descendants(window).ToArray();
   Assert.Contains(controls,c=>c is Button && c.Text=="Save & apply");
   ((CheckBox)controls.Single(c=>c.Text=="Enable touch independence")).Checked=true;
   window.Show(); window.PerformLayout(); for(var i=0;i<30;i++){Application.DoEvents();Thread.Sleep(20);}
   using var bitmap=new Bitmap(window.Width,window.Height);window.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));
   var path=Environment.GetEnvironmentVariable("TOUCHPILOT_SCREENSHOT");if(path is not null)bitmap.Save(path);
  }catch(Exception e){failure=e;}});
  thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.True(thread.Join(TimeSpan.FromSeconds(10)));Assert.Null(failure);
 }
 static IEnumerable<Control> Descendants(Control root){foreach(Control child in root.Controls){yield return child;foreach(var c in Descendants(child))yield return c;}}
}

