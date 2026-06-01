export interface Voice {
    id: string;
    name: string;
    language: string;
    quality: string;
}

export interface Chunk {
    index: number;
    hash: string;
    text: string;
}

export interface PrepareResponse {
    voice: string;
    chunks: Chunk[];
}
