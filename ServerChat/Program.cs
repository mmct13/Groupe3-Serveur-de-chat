using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Programme
{
    static readonly List<(TcpClient client, string username)> clients = new();
    static readonly object verrouClients = new();
    static readonly int maxClients = 5;

    static void Main(string[] args)
    {
        TcpListener serveur = new TcpListener(IPAddress.Any, 8000);
        serveur.Start();
        Console.WriteLine("[INFO] Serveur démarré sur le port 8000...");

        while (true)
        {
            TcpClient client = serveur.AcceptTcpClient();

            lock (verrouClients)
            {
                if (clients.Count >= maxClients)
                {
                    Console.WriteLine("[AVERTISSEMENT] Connexion refusée : limite de clients atteinte.");
                    client.Close();
                    continue;
                }
            }

            Console.WriteLine($"[NOUVELLE CONNEXION] Client depuis {((IPEndPoint)client.Client.RemoteEndPoint).Address}");
            Thread threadClient = new(() => GérerNouvelleConnexion(client));
            threadClient.Start();
        }
    }

    static void GérerNouvelleConnexion(TcpClient client)
    {
        try
        {
            NetworkStream flux = client.GetStream();
            byte[] tampon = new byte[1024];
            int octetsLus = flux.Read(tampon, 0, tampon.Length);
            string username = Encoding.UTF8.GetString(tampon, 0, octetsLus).Trim();

            if (string.IsNullOrWhiteSpace(username))
            {
                Console.WriteLine("[AVERTISSEMENT] Connexion rejetée : nom d'utilisateur vide.");
                client.Close();
                return;
            }

            lock (verrouClients)
            {
                clients.Add((client, username));
            }

            Console.WriteLine($"[INFO] {username} a rejoint le chat.");
            DiffuserMessage($"[SERVEUR] {username} a rejoint le chat.", null);
            GérerClient(client, username);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERREUR] Problème de connexion : {ex.Message}");
            client.Close();
        }
    }

    static void GérerClient(TcpClient client, string username)
    {
        try
        {
            NetworkStream flux = client.GetStream();
            byte[] tampon = new byte[1024];
            int octetsLus;

            while ((octetsLus = flux.Read(tampon, 0, tampon.Length)) != 0)
            {
                string message = Encoding.UTF8.GetString(tampon, 0, octetsLus).Trim();
                Console.WriteLine($"[MESSAGE] {message}");
                DiffuserMessage($" {message}", client);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERREUR] Problème avec {username}: {ex.Message}");
        }
        finally
        {
            lock (verrouClients)
            {
                clients.RemoveAll(c => c.client == client);
            }

            Console.WriteLine($"[INFO] {username} s'est déconnecté.");
            DiffuserMessage($"[SERVEUR] {username} a quitté le chat.", null);
            client.Close();
        }
    }

    static void DiffuserMessage(string message, TcpClient expediteur)
    {
        byte[] messageOctets = Encoding.UTF8.GetBytes(message);

        lock (verrouClients)
        {
            foreach (var client in clients)
            {
                try
                {
                    if (client.client != expediteur)
                    {
                        NetworkStream flux = client.client.GetStream();
                        flux.Write(messageOctets, 0, messageOctets.Length);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERREUR] Envoi à {client.username} échoué : {ex.Message}");
                }
            }
        }
    }
}
