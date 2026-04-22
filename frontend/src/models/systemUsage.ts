export interface StorageDrive {
    name: string;
    size: number;
    inUse: number;
}

export interface SystemUsage {
    cpu: number;
    gpu: number;
    ram: number;
    networkIn: number;
    networkOut: number;
    storage: StorageDrive[];
}