# P2P File Transfer

A lightweight and fast peer to peer file transfer application built in C# and WPF (Windows Presentation Foundation). This application is a tool that allows users to find other computers that are on the application and on the same local network and transfer files of any size without the need of external servers, internet bandwidth, or cloud storage.

## ✨ Features

* **UDP Auto Discovery:** Automatically broadcasts and listens for peers on the local network. There is no need to manually enter an IP address. Just open the app and see who is online.
* **Reliable TCP File Transfer:** Uses raw TCP sockets to guarantee orderly, complete, and reliable data delivery across the network.
* **Memory Efficient Streaming:** Safely handles files of any size by chunking data through an 8KB memory buffer rather than loading the entire file into RAM.
* **Metadata Transmission:** Preserves the original file name and extension by sending a metadata header before the raw file bytes, automatically routing the payload to the receiver's `Downloads` folder.
* **Cryptographic Integrity:** Calculates a SHA-256 hash on both the sender's original file and the receiver's newly built file to mathematically guarantee zero data corruption during transit.
* **Asynchronous UI:** Implements robust multithreading (`async/await`, `Task.Run`, and `Dispatcher`) to ensure the desktop interface remains completely responsive during heavy background network operations.

## 🛠️ Technologies & Architecture

* **Language:** C#
* **Framework:** .NET 8.0, Windows Presentation Foundation (WPF)
* **Networking:** `System.Net.Sockets` (TCP/UDP), `System.Net`
* **I/O & Streams:** `System.IO` (`FileStream`, `NetworkStream`, `BinaryReader/Writer`)
* **Security:** `System.Security.Cryptography` (SHA-256)

## 🚀 How to Run
### Option 1: Download & Run
1. Navigate to the **[Releases](https://github.com/NourElshabasy/P2PFileTransfer/releases)** tab on the right side of this GitHub page.
2. Download the latest `P2PFileTransfer.exe` file.
3. Double-click the `.exe` to run the application.
4. If Windows promts "Windows Protected Your PC" click "more info" and then click "run anyway"

### Option 2: Run via .NET CLI
If you have the .NET SDK installed, you can clone the repository and run the app directly from your terminal:
```bash
git clone https://github.com/NourElshabasy/P2PFileTransfer.git
cd P2PFileTransfer
dotnet run
```

### Option 3: Build a Standalone Executable
To generate a single, portable `.exe` file that can be run on any 64-bit Windows machine without needing the .NET SDK installed:
```
dotnet publish -c Release -r win-x64 /p:PublishSingleFile=true
```
You will find the compiled application in the `bin/Release/net8.0-windows/win-x64/publish` directory.

>Note: On the first run, Windows Defender Firewall may prompt you to allow the application to communicate over private networks. You must allow this for the UDP Auto-Discovery and TCP transfers to function.

## 📖 Usage

1. Open the application on two different Windows machines connected to the same local network.

2. The UI will automatically add to the list of available peers using their computer names and IP addresses.

3. On the Receiving machine: Click "Start Receiving" to open Port 5000 and listen for incoming connections.

4. On the Sending machine: Select the target peer from the list, click "Select File & Send", and choose your file.

5. The file will stream across the network and save automatically to the receiver's default Downloads folder with its original name.