# 🧠 Understanding Prometheus (With Simple Examples)

## 🧍 Non-Technical Analogy: The Security Guard

Imagine a **security guard** who walks around a building every 15 minutes and records important information:

- The **temperature** in each room
- The **number of people**
- Whether the **lights are on or off**

Each room has a digital display outside the door that always shows this data. The guard simply **writes it down** in a **logbook**, every time they pass by.

If something seems off—like the temperature being too high—the guard **sends an alert** (SMS, phone call, etc.) to the building manager.

### Key Analogy Mapping

| Concept         | Real-Life Analogy                        |
|-----------------|------------------------------------------|
| Prometheus      | Security guard with a logbook            |
| Metrics Exporter| Digital display outside each room        |
| Time-Series DB  | Logbook storing data with timestamps     |
| PromQL          | Asking questions about the logbook       |
| Alerting        | Guard notifies manager if rules break    |
| Grafana         | Dashboard in the security office showing trends visually |

---

## 🖥️ Technical Example: Monitoring a Web Application

### 🧱 Scenario Overview

You're running a modern web application that has:

- A **frontend** (React app)
- A **backend API** (Node.js or Python)
- A **PostgreSQL database**
- A **Linux server** (Ubuntu or CentOS)

You want to:

- Track **CPU and memory usage**
- Monitor **HTTP request latency and errors**
- Get **alerts** when things go wrong
- See data visually on a dashboard

---

### 🛠️ Step-by-Step Setup

1. **Prometheus** is installed and configured to **scrape** metrics from different services every 15 seconds.

2. Each service **exposes metrics** via a `/metrics` endpoint using an **exporter**:
   - `Node Exporter` for system metrics (CPU, RAM, disk)
   - `Blackbox Exporter` for pinging services (uptime checks)
   - Custom code metrics using client libraries (e.g., `prometheus-client` in Python)

3. Prometheus **stores** the data in its **time-series database**.

4. You can run **PromQL queries** to ask questions like:
   ``` promql
   rate(http_requests_total{status="500"}[5m])
   ```

5. Alertmanager (used with Prometheus) watches for alert rules like:
   - alert: HighCPUUsage
  expr: node_cpu_seconds_total > 90
  for: 5m
  labels:
    severity: critical
  annotations:
    summary: "CPU usage high for 5 minutes"

7. If an alert rule is triggered, Alertmanager can:
  Send notifications via email, Slack, PagerDuty, etc.

### Grafana: Visualization
Grafana is a visualization tool that connects to Prometheus to show the data in beautiful, interactive dashboards.

- With Grafana, you can:
- Create dashboards showing:
- CPU usage over time
- Memory consumption
- HTTP request error rates
- Database response times
- Set up thresholds and panel alerts
- Share dashboards with your team

# Summary
- Prometheus is like a guard that collects and logs information regularly.

- It pulls data from exporters and stores it in a time-series format.

- It can alert you when something breaks.

- Grafana provides a beautiful, visual interface to monitor all this data live.

- Together, Prometheus + Grafana give you a powerful monitoring system for modern cloud-native applications.
