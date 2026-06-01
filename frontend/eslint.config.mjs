import eslint from "@eslint/js";
import tseslint from "typescript-eslint";
import pluginVue from "eslint-plugin-vue";
import vueParser from "vue-eslint-parser";
import prettier from "eslint-plugin-prettier";
import configPrettier from "eslint-config-prettier";

export default tseslint.config(
    { ignores: ["dist/**", "node_modules/**", "../backend/wwwroot/**"] },

    eslint.configs.recommended,
    ...tseslint.configs.recommended,
    ...pluginVue.configs["flat/recommended"],
    configPrettier,

    {
        files: ["**/*.vue"],
        languageOptions: {
            parser: vueParser,
            parserOptions: {
                parser: tseslint.parser,
                extraFileExtensions: [".vue"],
            },
        },
    },

    {
        files: ["**/*.{ts,vue}"],
        plugins: { prettier },
        rules: {
            "prettier/prettier": [
                "warn",
                {
                    tabWidth: 4,
                    printWidth: 120,
                    endOfLine: "auto",
                    trailingComma: "es5",
                },
            ],

            // TypeScript
            "@typescript-eslint/no-explicit-any": "off",
            "@typescript-eslint/no-unused-vars": ["warn", { argsIgnorePattern: "^_" }],

            // Vue
            "vue/multi-word-component-names": "off",
            "vue/component-name-in-template-casing": ["error", "kebab-case", { registeredComponentsOnly: false }],
            "vue/v-on-event-hyphenation": ["error", "always", { ignore: ["update:modelValue"] }],
            "vue/require-default-prop": "off",
        },
    }
);
