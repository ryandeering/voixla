<script setup lang="ts">
import { computed, onMounted } from "vue";
import { usePlayer } from "@/composables/usePlayer";
import { useThemeToggle } from "@/composables/useThemeToggle";
import type { Voice } from "@/types";

const { isDark, toggle: toggleTheme } = useThemeToggle();

const player = usePlayer();
const {
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
} = player;

onMounted(player.loadVoices);

const voiceItems = computed(() =>
    voices.value.map((v) => ({
        value: v.id,
        title: prettyName(v),
        subtitle: `${accent(v.language)} · ${v.quality}`,
    }))
);

const playLabel = computed(() => {
    if (isLoading.value) {
        return "Preparing…";
    }
    if (isPlaying.value) {
        return "Pause";
    }
    return hasChunks.value && !isDirty.value ? "Resume" : "Read aloud";
});

const playIcon = computed(() => (isPlaying.value ? "mdi-pause" : "mdi-play"));

function prettyName(v: Voice): string {
    return v.name.split(" (")[0] || v.id;
}

function accent(lang: string): string {
    const map: Record<string, string> = { en_US: "US English", en_GB: "British English" };
    return map[lang] ?? lang;
}
</script>

<template>
    <v-app>
        <v-app-bar flat color="surface" border="b">
            <v-container class="d-flex align-center py-0" style="max-width: 920px">
                <v-icon icon="mdi-text-to-speech" color="primary" size="28" class="me-3" />
                <span class="text-h6 font-weight-medium">Voixla</span>
                <span class="text-medium-emphasis ms-3 d-none d-sm-inline">Read your writing aloud</span>
                <v-spacer />
                <v-btn
                    :icon="isDark ? 'mdi-weather-sunny' : 'mdi-weather-night'"
                    variant="text"
                    :title="isDark ? 'Switch to light mode' : 'Switch to dark mode'"
                    @click="toggleTheme"
                />
            </v-container>
        </v-app-bar>

        <v-main class="bg-background">
            <v-container class="py-10" style="max-width: 920px">
                <v-card elevation="1" border>
                    <v-card-text class="pa-8">
                        <v-row dense>
                            <v-col cols="12" sm="7">
                                <v-select
                                    v-model="voice"
                                    :items="voiceItems"
                                    item-props
                                    label="Voice"
                                    prepend-inner-icon="mdi-account-voice"
                                    hide-details="auto"
                                    no-data-text="No voices installed"
                                />
                            </v-col>
                            <v-col cols="12" sm="5">
                                <div class="text-caption text-medium-emphasis mb-1">Speed · {{ rate.toFixed(1) }}×</div>
                                <v-slider
                                    v-model="rate"
                                    :min="0.7"
                                    :max="1.5"
                                    :step="0.1"
                                    color="primary"
                                    prepend-icon="mdi-speedometer"
                                    hide-details
                                />
                            </v-col>
                        </v-row>

                        <v-textarea
                            v-model="text"
                            class="mt-6"
                            label="Paste a chapter here…"
                            auto-grow
                            rows="10"
                            max-rows="20"
                            spellcheck="false"
                            hide-details
                        />

                        <div class="d-flex align-center flex-wrap ga-3 mt-6">
                            <v-btn
                                color="primary"
                                size="large"
                                :loading="isLoading"
                                :disabled="!text.trim() || !voice"
                                :prepend-icon="playIcon"
                                @click="player.toggle"
                            >
                                {{ playLabel }}
                            </v-btn>
                            <v-btn
                                variant="tonal"
                                size="large"
                                :disabled="!hasChunks"
                                prepend-icon="mdi-stop"
                                @click="player.stop"
                            >
                                Stop
                            </v-btn>
                            <v-spacer />
                            <span v-if="progressLabel" class="text-medium-emphasis">{{ progressLabel }}</span>
                        </div>
                    </v-card-text>
                </v-card>

                <v-alert
                    v-if="error"
                    type="error"
                    variant="tonal"
                    class="mt-6"
                    :text="error"
                    closable
                    @click:close="error = ''"
                />

                <v-card v-if="hasChunks" elevation="1" border class="mt-6">
                    <v-list class="py-0">
                        <template v-for="(c, i) in chunks" :key="c.hash">
                            <v-divider v-if="i > 0" />
                            <v-list-item
                                :active="c.index === currentIndex"
                                active-color="primary"
                                class="py-3"
                                @click="player.jumpTo(c.index)"
                            >
                                <template #prepend>
                                    <v-icon
                                        :icon="
                                            c.index === currentIndex && isPlaying
                                                ? 'mdi-volume-high'
                                                : 'mdi-play-circle-outline'
                                        "
                                        :color="c.index === currentIndex ? 'primary' : 'medium-emphasis'"
                                        class="me-1"
                                    />
                                </template>
                                <v-list-item-text class="text-body-1" style="white-space: normal">
                                    {{ c.text }}
                                </v-list-item-text>
                            </v-list-item>
                        </template>
                    </v-list>
                </v-card>
            </v-container>
        </v-main>

        <v-footer color="surface" border="t" class="flex-grow-0 justify-center py-4">
            <span class="text-caption text-medium-emphasis text-center">
                © 2026 Ryan Deering · Speech synthesis by
                <a
                    href="https://github.com/OHF-Voice/piper1-gpl"
                    target="_blank"
                    rel="noopener noreferrer"
                    class="text-primary text-decoration-none"
                    >Piper</a
                >
            </span>
        </v-footer>
    </v-app>
</template>
