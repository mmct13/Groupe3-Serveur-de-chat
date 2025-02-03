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
        Console.WriteLine("Serveur demarre sur le port 8000");

        while (true)
        {
            TcpClient client = serveur.AcceptTcpClient();
            Console.WriteLine("Nouveau client connecte");

            // Demander le nom d'utilisateur
            NetworkStream flux = client.GetStream();
            byte[] tampon = new byte[1024];
            int octetsLus = flux.Read(tampon, 0, tampon.Length);
            string username = Encoding.UTF8.GetString(tampon, 0, octetsLus).Trim();

            // Ajouter le client à la liste avec son nom d'utilisateur
            clients.Add((client, username));
            Console.WriteLine($"{username} a rejoint le chat.");

            Thread threadClient = new(() => GererClient(client, username));
            threadClient.Start();
        }
    }

    static void GererClient(TcpClient client, string username)
    {
        NetworkStream flux = client.GetStream();
        byte[] tampon = new byte[1024];
        int octetsLus;

        while ((octetsLus = flux.Read(tampon, 0, tampon.Length)) != 0)
        {
            string message = Encoding.UTF8.GetString(tampon, 0, octetsLus);
            Console.WriteLine($"Reçu de {username}: {message}");

            // Diffuser le message en incluant le nom d'utilisateur
            DiffuserMessage($"{username}: {message}", client);
        }

        // Lorsque le client se déconnecte
        clients.RemoveAll(c => c.client == client);
        client.Close();
    }

    static void DiffuserMessage(string message, TcpClient expediteur)
    {
        byte[] messageOctets = Encoding.UTF8.GetBytes(message);

        // Diffuser le message à tous les clients sauf à l'expéditeur
        foreach (var client in clients)
        {
            if (client.client != expediteur)
            {
                NetworkStream flux = client.client.GetStream();
                flux.Write(messageOctets, 0, messageOctets.Length);
            }
        }
    }
}
