using System;
using Org.Mentalis.Utilities;

public class TestApp {
	public static void Main() {
		Console.WriteLine("Restart Program\r\n--------------\r\n\r\n Select the restart mode:\r\n   l - LogOff\r\n   p - PowerOff\r\n   r - Reboot\r\n   s - Shutdown\r\n   u - Suspend\r\n   h - Hibernate\r\n   c - Exit the program\r\n\r\n Make your choice: ");
		string input = Console.ReadLine();
		switch(input.ToLower()) {
			case "l":
				WindowsController.ExitWindows(RestartOptions.LogOff, false);
				break;
			case "p":
				WindowsController.ExitWindows(RestartOptions.PowerOff, false);
				break;
			case "r":
				WindowsController.ExitWindows(RestartOptions.Reboot, false);
				break;
			case "s":
				WindowsController.ExitWindows(RestartOptions.ShutDown, false);
				break;
			case "u":
				WindowsController.ExitWindows(RestartOptions.Suspend, false);
				break;
			case "h":
				WindowsController.ExitWindows(RestartOptions.Hibernate, false);
				break;
			case "c":
				return;
			default:
				Console.WriteLine("Invalid input...");
				break;
		}
	}
}