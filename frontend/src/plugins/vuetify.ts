import "vuetify/styles";
import { createVuetify } from "vuetify";
import * as components from "vuetify/components";
import * as directives from "vuetify/directives";

const voixla = {
    dark: false,
    colors: {
        background: "#f8f9fa",
        surface: "#ffffff",
        primary: "#1a73e8",
        secondary: "#5f6368",
        error: "#d93025",
        success: "#188038",
    },
};

const voixlaDark = {
    dark: true,
    colors: {
        background: "#202124",
        surface: "#292a2d",
        primary: "#8ab4f8",
        secondary: "#9aa0a6",
        error: "#f28b82",
        success: "#81c995",
    },
};

const prefersDark = window.matchMedia?.("(prefers-color-scheme: dark)").matches ?? false;

export default createVuetify({
    components,
    directives,
    icons: { defaultSet: "mdi" },
    theme: {
        defaultTheme: prefersDark ? "voixlaDark" : "voixla",
        themes: { voixla, voixlaDark },
    },
    defaults: {
        VCard: { rounded: "lg" },
        VBtn: { rounded: "lg" },
        VTextField: { variant: "outlined" },
        VTextarea: { variant: "outlined" },
        VSelect: { variant: "outlined" },
    },
});
