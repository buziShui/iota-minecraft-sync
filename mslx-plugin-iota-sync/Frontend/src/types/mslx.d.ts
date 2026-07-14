declare module 'mslx-request' {
    const request: any;
    export default request;
}

declare global {
    interface Window {
        MSLX_Stores: any;
        MSLX_API: any;
        mslxRequest: any;
    }
}

declare module '*.vue' {
    import type { DefineComponent } from 'vue';
    // eslint-disable-next-line @typescript-eslint/no-explicit-any, @typescript-eslint/ban-types
    const component: DefineComponent<{}, {}, any>;
    export default component;
}