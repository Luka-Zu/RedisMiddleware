export interface ServerMetric {
  timestamp: string;
  // CPU
  usedCpuSys: number;
  usedCpuUser: number;
  // Memory
  usedMemory: number;      // Bytes
  usedMemoryRss: number;   // Bytes
  fragmentationRatio: number;
  evictedKeys: number;
  // Network
  inputKbps: number;
  outputKbps: number;
  // Cache Stats
  keyspaceHits: number;
  keyspaceMisses: number;
  // General
  connectedClients: number;
  opsPerSec: number;
}