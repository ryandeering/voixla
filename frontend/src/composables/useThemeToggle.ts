import { computed } from "vue";
import { useTheme } from "vuetify";

const LIGHT = "voixla";
const DARK = "voixlaDark";
const STORAGE_KEY = "voixla-theme";

export function useThemeToggle() {
    const theme = useTheme();

    const saved = localStorage.getItem(STORAGE_KEY);
    if (saved === LIGHT || saved === DARK) {
        theme.global.name.value = saved;
    }

    const isDark = computed(() => theme.global.current.value.dark);

    function toggle(): void {
        const next = isDark.value ? LIGHT : DARK;
        theme.global.name.value = next;
        localStorage.setItem(STORAGE_KEY, next);
    }

    return { isDark, toggle };
}
