export interface RequestLog {
  timestamp: string;
  command: string;
  key: string;
  latencyMs: number;
  isSuccess: boolean;
  isHit: boolean;
  payloadSize: number;
}