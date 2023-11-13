using System;
using System.Collections;
using System.Net;
using UnityEngine;

public class InternetVerifier : MonoBehaviour
{
    private string _linkURL;
    
    public void CheckConnection()
    {
        string m_ReachabilityText = "";
     
        //Check if the device cannot reach the internet at all (that means if the "cable", "WiFi", etc. is connected or not)
        //if not, don't waste your time.
        if (Application.internetReachability == NetworkReachability.NotReachable)
        {
            m_ReachabilityText = "Not Connected.";
            Debug.Log("Internet : " + m_ReachabilityText);
        }
        else
        {
            StartCoroutine(DoPing()); //It could be a network connection but not internet access so you have to ping your host/server to be sure.
        }
    }
 
    IEnumerator DoPing()
    {
        Debug.Log("Do Ping");
        TestPing.DoPing();
        yield return new WaitUntil(() => TestPing.IsDone);
        var connected = TestPing.Status;
 
        if (connected)
        {
            Debug.Log("Connected");
            //Do your thing once the connection is confirmed
            Debug.Log(TestPing.IpAdd); // just to be sure if that is your IP
        }
        else
        {
            //if negative result awarn your user
            //and do your thing with this result
            Debug.Log(TestPing.IpAdd);
            Debug.Log("Please check your network connections or network permissions");
        }
    }
}

public static class TestPing
{
    public static bool Status = false;
    public static bool IsDone = false;
    public static string IpAdd; //The IP address for the ping call

    private static bool PingThis()
    {
        try
        {
            //I strongly recommend to check Ping, Ping.Send & PingOptions on microsoft C# docu or other C# info source
            //in this block you configure the ping call to your host or server in order to check if there is network connection.
         
            //from https://stackoverflow.com/questions/55461884/how-to-ping-for-ipv4-only
            //from https://stackoverflow.com/questions/49069381/why-ping-timeout-is-not-working-correctly
            //and from https://stackoverflow.com/questions/2031824/what-is-the-best-way-to-check-for-internet-connectivity-using-net
         
         
            var myPing = new System.Net.NetworkInformation.Ping();
         
            var buffer = new byte[32]; //array that contains data to be sent with the ICMP echo
            var timeout = 10000; //in milliseconds
            var pingOptions = new System.Net.NetworkInformation.PingOptions(64, true);
            var reply = myPing.Send(IpAdd, timeout, buffer, pingOptions); //the same method can be used without the timeout, data buffer & pingOptions overloadd but this works for me
            return reply != null && reply.Status switch
            {
                System.Net.NetworkInformation.IPStatus.Success => true,
                //to handle the timeout scenario
                System.Net.NetworkInformation.IPStatus.TimedOut => Status,
                _ => false
            };
        }
        catch (Exception e) //To catch any exception of the method
        {
            Debug.Log(e);
            return false;
        }
    }
 
    public static string GetIPAddress() //Get the actual IP addres of your host/server
    {
        //Yes, I could use the "host name" or the "host IP address" direct on the ping.send method BUT!!
        //I find out and "Situation" in which due to my network setting in my PC any ping call (from script or cmd console)
        //returned the IPv6 instead of IPv4 which couse the Ping.Send thrown an exception
        //that could be the scenario for many of your users so you have to ensure this run for everyone.
     
     
        //from https://stackoverflow.com/questions/1059526/get-ipv4-addresses-from-dns-gethostentry
     
        IPHostEntry host;
        host = Dns.GetHostEntry("google.com"); //I use google.com as an example but it can be any host name (preferably yours)
 
        try
        {
            host = Dns.GetHostEntry("google.com"); //Get the IP host entry from your host/server
        }
        catch (Exception e)
        {
            Debug.Log(e);
        }

        foreach (var ip in host.AddressList)
        {
            if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork) //filter just the IPv4 IPs
            {                                                                      //you can play around with this and get all the IP arrays (if any)
                return ip.ToString();                                              //and check the connection with all of then if needed
            }
        }
        return string.Empty;
    }
 
    public static void DoPing()
    {
        IpAdd = GetIPAddress(); //call to get the IP address from your host/server
 
        if (PingThis()) //call to check if you can make ping to that host IP
        {
            Status = true;
            IsDone = true;
        }
        else
        {
            Status = false;
            IsDone = true;
        }
    }
}
