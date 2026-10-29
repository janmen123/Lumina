using System;using GlassBoard;class Check{[STAThread]static int Main(){try{PersonalizationChecks.Run();Console.WriteLine("PASS");return 0;}catch(Exception ex){Console.WriteLine(ex);return 1;}}}
