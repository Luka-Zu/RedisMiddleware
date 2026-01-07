# RedisInsight-Middleware (Backend)

## 📌 Project Overview
This project is a **High-Performance Redis Middleware & Proxy** designed for traffic analysis and observability. It sits between the client application and the Redis server, performing real-time Deep Packet Inspection (DPI) on the RESP protocol.

It features a **Dual-Worker Architecture**:
1.  **TCP Proxy Worker:** Intercepts traffic on port `6380`, parses commands, measures latency, and correlates request/response cycles.
2.  **Monitor Worker:** Periodically polls the Redis server (via `INFO` command) for system health metrics (CPU, RAM, Clients).

All data is aggregated and persisted to **PostgreSQL** for historical analysis and broadcasted in real-time via **SignalR**.

---

## 🏗️ Architecture

* **Language:** C# .NET 8 (Worker Service + Web API)
* **Database:** PostgreSQL 15
* **Cache/Target:** Redis (Alpine)
* **Real-Time:** SignalR (WebSockets)
* **ORM:** Dapper (High-performance micro-ORM)

---

## 🚀 Prerequisites

1.  **Docker Desktop** (Must be running).
2.  **.NET 8 SDK** installed.
3.  **IDE:** Rider (Recommended), Visual Studio, or VS Code.

---

## 🛠️ How to Run (Step-by-Step)

### 1. Start Infrastructure (Docker)
We use Docker Compose to spin up the Database and the Real Redis Server.

1.  Open your terminal in the solution root folder.
2.  Run the infrastructure:
    ```bash
    docker-compose up -d
    ```
3.  Verify containers are running:
    ```bash
    docker ps
    ```
    * `redis_thesis` (Port 6379)
    * `postgres_thesis` (Port 5433 -> 5432 internal)
    * `pgadmin_thesis` (Port 5050 - Optional UI)

### 2. Configure the Backend
Ensure `appsettings.json` matches your Docker configuration:

```json
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5433;Database=redis_metrics_db;Username=postgres;Password=password"
},
"Kestrel": {
  "Endpoints": {
    "Http": {
      "Url": "http://localhost:5000"
    }
  }
}