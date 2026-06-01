import { createApp } from "vue";
import App from "@/App.vue";
import vuetify from "@/plugins/vuetify";

import "@fontsource/roboto/300.css";
import "@fontsource/roboto/400.css";
import "@fontsource/roboto/500.css";
import "@fontsource/roboto/700.css";
import "@mdi/font/css/materialdesignicons.css";

createApp(App).use(vuetify).mount("#app");
