using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

class Programme
{
    // Liste des clients connectés et leurs noms d'utilisateur
    static List<(TcpClient client, string username)> clients = new List<(TcpClient, string)>();

    static void Main(string[] args)
    {
        TcpListener serveur = new TcpListener(IPAddress.Any, 8000);
        serveur.Start();
        Console.WriteLine("[INFO] Serveur démarré sur le port 8000...");

        while (true)
        {
            TcpClient client = serveur.AcceptTcpClient();
            Console.WriteLine($"[NOUVELLE CONNEXION] Un client s'est connecté depuis {((IPEndPoint)client.Client.RemoteEndPoint).Address}");

            // Demander le nom d'utilisateur
            NetworkStream flux = client.GetStream();
            byte[] tampon = new byte[1024];
            int octetsLus = flux.Read(tampon, 0, tampon.Length);
            string username = Encoding.UTF8.GetString(tampon, 0, octetsLus).Trim();

            if (string.IsNullOrWhiteSpace(username))
            {
                Console.WriteLine("[AVERTISSEMENT] Connexion rejetée : nom d'utilisateur vide.");
                client.Close();
                continue;
            }

            // Ajouter le client à la liste avec son nom d'utilisateur
            clients.Add((client, username));
            Console.WriteLine($"[INFO] {username} ({((IPEndPoint)client.Client.RemoteEndPoint).Address}) a rejoint le chat.");

            // Annoncer l'arrivée du nouvel utilisateur à tous
            DiffuserMessage($"[SERVEUR] {username} a rejoint le chat.", null);

            Thread threadClient = new(() => GererClient(client, username));
            threadClient.Start();
        }
    }

    static void GererClient(TcpClient client, string username)
    {
        try
        {
            NetworkStream flux = client.GetStream();
            byte[] tampon = new byte[1024];
            int octetsLus;

            while ((octetsLus = flux.Read(tampon, 0, tampon.Length)) != 0)
            {
                string message = Encoding.UTF8.GetString(tampon, 0, octetsLus).Trim();
                Console.WriteLine($"[MESSAGE] {username}: {message}");

                // Diffuser le message en incluant le nom d'utilisateur
                DiffuserMessage($"{username}: {message}", client);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ERREUR] Problème avec {username}: {ex.Message}");
        }
        finally
        {
            // Lorsque le client se déconnecte
            clients.RemoveAll(c => c.client == client);
            Console.WriteLine($"[INFO] {username} s'est déconnecté.");
            DiffuserMessage($"[SERVEUR] {username} a quitté le chat.", null);
            client.Close();
        }
    }

    static void DiffuserMessage(string message, TcpClient expediteur)
    {
        byte[] messageOctets = Encoding.UTF8.GetBytes(message);

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
                Console.WriteLine($"[ERREUR] Impossible d'envoyer un message à {client.username}: {ex.Message}");
            }
        }
    }
}
