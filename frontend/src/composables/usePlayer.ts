import { computed, onUnmounted, ref, watch } from "vue";
import { fetchVoices, prepareChapter } from "@/api/tts";
import type { Chunk, Voice } from "@/types";

const PREFETCH_AHEAD = 2;

export function usePlayer() {
    const text = ref("");
    const voices = ref<Voice[]>([]);
    const voice = ref("");
    const rate = ref(1);

    const chunks = ref<Chunk[]>([]);
    const currentIndex = ref(-1);
    const isPlaying = ref(false);
    const isLoading = ref(false);
    const error = ref("");

    const preparedKey = ref("");
    const keyFor = (v: string, t: string) => `${v}|${t}`;

    // Generation counter + abort handle so a superseded/cancelled prepare can't start audio.
    let loadToken = 0;
    let inFlight: AbortController | null = null;

    // Bumped on every play/stop so a late audio.play() promise can't clobber newer state.
    let playToken = 0;

    const audio = new Audio();
    audio.preload = "auto";

    // Resolve audio URLs against the Vite base so they work at root or under a sub-path.
    const audioUrl = (chunk: Chunk) => `${import.meta.env.BASE_URL}api/audio/${chunk.hash}.wav`;

    const hasChunks = computed(() => chunks.value.length > 0);
    const isDirty = computed(() => keyFor(voice.value, text.value) !== preparedKey.value);
    const progressLabel = computed(() =>
        hasChunks.value ? `Part ${Math.min(currentIndex.value + 1, chunks.value.length)} of ${chunks.value.length}` : ""
    );

    watch(rate, (r) => {
        audio.playbackRate = r;
    });

    const onEnded = () => {
        if (currentIndex.value + 1 < chunks.value.length) {
            playFrom(currentIndex.value + 1);
        } else {
            isPlaying.value = false;
            currentIndex.value = -1;
        }
    };
    audio.addEventListener("ended", onEnded);
    onUnmounted(() => {
        audio.removeEventListener("ended", onEnded);
        stop();
    });

    async function loadVoices(): Promise<void> {
        try {
            voices.value = await fetchVoices();
            if (voices.value.length && !voice.value) {
                voice.value = preferredVoice(voices.value);
            }
        } catch {
            error.value = "Could not reach the server.";
        }
    }

    function preferredVoice(list: Voice[]): string {
        const score = (v: Voice) =>
            (v.language.startsWith("en") ? 2 : 0) + (v.quality === "high" ? 2 : v.quality === "medium" ? 1 : 0);
        return [...list].sort((a, b) => score(b) - score(a))[0].id;
    }

    async function read(): Promise<void> {
        const reqText = text.value;
        const reqVoice = voice.value;
        if (!reqText.trim() || !reqVoice) {
            return;
        }
        stop();
        const token = ++loadToken;
        inFlight = new AbortController();
        isLoading.value = true;
        error.value = "";
        try {
            const data = await prepareChapter(reqText, reqVoice, inFlight.signal);
            if (token !== loadToken) {
                return;
            }
            chunks.value = data.chunks;
            preparedKey.value = keyFor(reqVoice, reqText);
            if (chunks.value.length) {
                playFrom(0);
            }
        } catch (e) {
            if (token !== loadToken) {
                return;
            }
            error.value = e instanceof Error ? e.message : "Something went wrong.";
        } finally {
            if (token === loadToken) {
                isLoading.value = false;
            }
        }
    }

    function playFrom(index: number): void {
        if (index < 0 || index >= chunks.value.length) {
            stop();
            return;
        }
        currentIndex.value = index;
        const token = ++playToken;
        audio.src = audioUrl(chunks.value[index]);
        audio.playbackRate = rate.value;
        audio.play().then(
            () => {
                if (token === playToken) {
                    isPlaying.value = true;
                }
            },
            () => {
                if (token === playToken) {
                    error.value = "Playback failed.";
                }
            }
        );
        prefetch(index + 1);
    }

    // Warm the browser cache for upcoming chunks; the GET is cached and reused as src.
    function prefetch(from: number): void {
        for (let i = from; i < Math.min(from + PREFETCH_AHEAD, chunks.value.length); i++) {
            void fetch(audioUrl(chunks.value[i])).catch(() => {});
        }
    }

    function toggle(): void {
        if (!hasChunks.value || isDirty.value) {
            void read();
            return;
        }
        if (isPlaying.value) {
            playToken++;
            audio.pause();
            isPlaying.value = false;
        } else if (currentIndex.value < 0) {
            playFrom(0);
        } else {
            void audio.play();
            isPlaying.value = true;
        }
    }

    function stop(): void {
        loadToken++;
        playToken++;
        inFlight?.abort();
        inFlight = null;
        audio.pause();
        audio.removeAttribute("src");
        isPlaying.value = false;
        isLoading.value = false;
        currentIndex.value = -1;
    }

    function jumpTo(index: number): void {
        playFrom(index);
    }

    return {
        text,
        voices,
        voice,
        rate,
        chunks,
        currentIndex,
        isPlaying,
        isLoading,
        error,
        hasChunks,
        isDirty,
        progressLabel,
        loadVoices,
        toggle,
        stop,
        jumpTo,
    };
}
