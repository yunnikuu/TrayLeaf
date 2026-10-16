using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;
class ControlledFixture:Form {
 bool finish,ignore; NotifyIcon tray; Timer timer;
 [STAThread] static int Main(string[] args) {
  var f=new ControlledFixture();f.Text="TrayLeaf controlled fixture";f.Width=420;f.Height=180;f.StartPosition=FormStartPosition.CenterScreen;
  f.ignore=Array.IndexOf(args,"--ignore-close")>=0;
  f.Controls.Add(new Label {Text="TrayLeaf 自动化验证专用窗口\n不读取或修改其他应用",Dock=DockStyle.Fill,TextAlign=System.Drawing.ContentAlignment.MiddleCenter});
  int log=Array.IndexOf(args,"--log");if(log>=0&&log+1<args.Length)File.WriteAllText(args[log+1],Process.GetCurrentProcess().Id+"\n"+Environment.CommandLine);
  if(Array.IndexOf(args,"--tray-only")>=0){f.ShowInTaskbar=false;f.WindowState=FormWindowState.Minimized;f.Opacity=0;f.tray=new NotifyIcon {Icon=System.Drawing.SystemIcons.Application,Text="TrayLeaf 验证专用托盘图标",Visible=true};f.Shown+=delegate{f.Hide();};}
  f.FormClosing+=delegate(object s,FormClosingEventArgs e){if(f.ignore&&!f.finish)e.Cancel=true;};
  f.timer=new Timer{Interval=90000};f.timer.Tick+=delegate{f.finish=true;f.Close();};f.timer.Start();
  try{Application.Run(f);return 0;}finally{if(f.tray!=null)f.tray.Dispose();f.timer.Dispose();}
 }
}
