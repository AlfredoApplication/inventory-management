using LoginAppFramework; // This is the namespace where PasswordHasher lives
using System;

// This is the entire content for your Program.cs file
namespace PasswordHasherTool
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("--- Inventory App Password Hash Generator ---");
            Console.WriteLine("This tool will create a secure password hash for your first user.");
            Console.WriteLine();

            Console.Write("Enter the password for your new user (e.g., 'admin123'): ");
            string myPassword = Console.ReadLine();

            if (string.IsNullOrWhiteSpace(myPassword))
            {
                Console.WriteLine("Password cannot be empty. Exiting.");
                return;
            }

            // Call the exact same method your application uses
            string hashedPassword = PasswordHasher.HashPassword(myPassword);

            Console.WriteLine("\n=======================================================");
            Console.WriteLine("SUCCESS! Password hash generated.");
            Console.WriteLine("COPY THE ENTIRE LINE OF TEXT BELOW:");
            Console.WriteLine("=======================================================");
            Console.WriteLine();

            // This will print the hash you need for your SQL script
            Console.WriteLine(hashedPassword);

            Console.WriteLine("\n\nPress any key to exit.");
            Console.ReadKey();
        }
    }
}