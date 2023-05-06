# Overview
This project demonstrates multithreading and synchronization among a few other concepts.

# Design
The application has a simple WPF-based GUI, and supports sending and receiving messages through UDP.
- The application runs a WPF based GUI.
- A worker thread is spawned to listen for messages on a UDP port.
- Another worker thread is spawned to summarize messages after a batch of messages has been transacted.
- A task is used to send messages to a UDP port.
- Synchronization primitives are used to make thread-safe access of shared resources.

# Concepts
This projects demonstrates the following concepts.
- **Multithreading**:
Multithreading allows to increase the responsiveness of an application and, if the application runs on a multiprocessor or multi-core system, increase its throughput.
  - *Thread*: The application creates two threads, one to listen for messages on the UDP port, and another to generate a summary after a batch of messages has been transacted.
  - *Task*: The application creates a task to send messages across the UDP port. *awaiting* on a task makes sure the UI is still responsive (as opposed to be blocked if the messages were sent on the UI thread).
- **Synchronization**:
When multiple threads are accessing shared data, a program must provide for possible resource sharing/conflicts. This is called synchronization.
  - *Event*: The application uses an autoreset event to signal the summarizer thread from the other threads.
  - *Lock/Monitor*: The application uses lock (internally calls Monitor) to provide a thread with exclusive access to a shared resource for executing a particular code path.
- **GUI programming**:
A program could use XAML and Windows Presentation Foundation (WPF) for a rich Graphical User Interface.
  - *WPF/XAML*: The application demonstrates how to use a simple WPF based GUI.
  - *UI element access only on UI threads*: The application demonstrates scheduling a callback on the UI thread from a worker thread. Any UI element access/manipulation must happen only from the UI thread.
- **IPC**:
Inter-Process Communication mechanisms allow different processes to communicate and manage shared data.
  - *UDP*: The application uses User Datagram Protocol (UDP) for IPC.

# Environment
The project builds and runs with Visual Studio Community 2022 when the required workloads are installed.
