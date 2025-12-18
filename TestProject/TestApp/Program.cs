namespace TestApp;

public class NotifierStrategy
{
    private string secretKey = "mySecretKey123";
    
    public string UserName { get; set; } = "DefaultUser";
    
    public bool Login(string username, string password)
    {
        if (VerifyCredentials(username, password))
        {
            UserName = username;
            return true;
        }
        return false;
    }
    
    private bool VerifyCredentials(string username, string password)
    {
        return password == secretKey;
    }
}

public class DataProcessor
{
    public int ProcessData(int[] values)
    {
        int result = 0;
        foreach (var value in values)
        {
            result += CalculateValue(value);
        }
        return result;
    }
    
    private int CalculateValue(int input)
    {
        return input * 2 + 10;
    }
}

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Test Application");
        
        var authService = new NotifierStrategy();
        bool loginSuccess = authService.Login("admin", "mySecretKey123");
        
        if (loginSuccess)
        {
            Console.WriteLine($"Welcome, {authService.UserName}!");
        }
        
        var processor = new DataProcessor();
        int[] data = { 1, 2, 3, 4, 5 };
        int result = processor.ProcessData(data);
        Console.WriteLine($"Processing result: {result}");
    }
}
