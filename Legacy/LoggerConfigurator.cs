using System;
using System.IO;
using log4net.Core;

using LoggerSystem.Core;
namespace BewerbungsAssiLogger.Helper;
public sealed record LoggerConfigurator
{
 public string FolderName {get;init;}=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"BewerbungsAssi","Logs");
 public string FileName {get;init;}="application.log";
 public string MaximumFileSize {get;init;}="5MB";
 public int AmountFilesBeforeBackup {get;init;}=10;
 public int MaxEntries {get;init;}=1000;
 public bool IsAppendToFile {get;init;}=true;
 public AppenderType AppenderTypes {get;init;}= AppenderType.RollingFile | AppenderType.Memory;
 public bool EnableConsole {get=>(AppenderTypes& AppenderType.Console)!=0;init=>AppenderTypes=value?AppenderTypes| AppenderType.Console:AppenderTypes&~AppenderType.Console;}
 public Level MinimumLevel {get;init;}=Level.Debug;
 public const string LayoutConversionPattern="%date{yyyy-MM-dd HH:mm:ss.fff} [%-5level] %logger - %message%newline%exception";
 public string LayoutPattern {get;init;}=LayoutConversionPattern;
 internal LoggerConfigurator Validate()
 {
  const AppenderType supported = AppenderType.RollingFile | AppenderType.Console | AppenderType.Memory;
  if(AppenderTypes==0||(AppenderTypes&~supported)!=0)throw new ArgumentException("Invalid appenders");
  ArgumentException.ThrowIfNullOrWhiteSpace(FolderName);ArgumentException.ThrowIfNullOrWhiteSpace(FileName);
  if(Path.GetFileName(FileName)!=FileName)throw new ArgumentException("FileName must not contain a path");
  if(!System.Text.RegularExpressions.Regex.IsMatch(MaximumFileSize,@"^[1-9]\d*(B|KB|MB|GB)$",System.Text.RegularExpressions.RegexOptions.IgnoreCase))throw new ArgumentException("Invalid size");
  if(AmountFilesBeforeBackup<0||MaxEntries<=0)throw new ArgumentException("Invalid limits");
  ArgumentNullException.ThrowIfNull(MinimumLevel);ArgumentException.ThrowIfNullOrWhiteSpace(LayoutPattern);
  return this with {FolderName=Path.GetFullPath(FolderName)};
 }
}
