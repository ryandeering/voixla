import { isAxiosError } from "axios";
import { apiClient } from "@/api/client";
import type { PrepareResponse, Voice } from "@/types";

export async function fetchVoices(): Promise<Voice[]> {
    const { data } = await apiClient.get<Voice[]>("/voices");
    return data;
}

export async function prepareChapter(text: string, voice: string, signal?: AbortSignal): Promise<PrepareResponse> {
    try {
        const { data } = await apiClient.post<PrepareResponse>("/prepare", { text, voice }, { signal });
        return data;
    } catch (e) {
        if (isAxiosError<{ error?: string }>(e)) {
            throw new Error(e.response?.data?.error ?? "Could not prepare audio.");
        }
        throw e;
    }
}
