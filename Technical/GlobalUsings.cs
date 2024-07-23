#if CROSS_PLATFORM

global using CrossColor = System.Drawing.Color;
global using CrossKey = Avalonia.Input.Key;
global using CrossColors = System.Drawing.Color;
global using CrossKeyEventArgs = Avalonia.Input.KeyEventArgs;

#else

global using CrossColor = System.Windows.Media.Color;
global using CrossKey = System.Windows.Input.Key;
global using CrossColors = System.Windows.Media.Colors;
global using CrossKeyEventArgs = System.Windows.Input.KeyEventArgs;

#endif