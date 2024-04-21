// uno.config.mjs
import { defineConfig, presetUno } from "unocss";

export default defineConfig({
    content: {
        filesystem: ["**/*.{html,js,ts,jsx,tsx,razor,cshtml}"],
    },
    presets: [presetUno()],
});
