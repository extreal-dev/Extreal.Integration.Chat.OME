import typescript from "@rollup/plugin-typescript";
import terser from "@rollup/plugin-terser";
import { RollupOptions } from "rollup";

const isProd = process.env.BUILD === "production";

const config: RollupOptions = {
    input: "src/index.ts",
    external: [
        "@extreal-dev/extreal.integration.web.common",
        "socket.io-client"
    ],
    output: {
        file: "dist/index.js",
        format: "es",
        plugins: isProd ? [terser()] : [],
    },
    plugins: [
        typescript({
            declaration: true,
            declarationDir: "types",
            exclude: "rollup.config.ts"
        }),
    ],
};

export default config;
