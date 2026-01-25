export interface Advisory {
    level: 'Critical' | 'Warning' | 'Info';
    message: string;
    command: string;
    timestamp: string;
}