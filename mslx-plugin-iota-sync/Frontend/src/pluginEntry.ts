import SyncAdmin from './views/SyncAdmin.vue';
export const pluginConfig = {
  name: 'mslx-plugin-iota-sync', version: '1.1.1',
  routes: [{ path: '/iota-sync', name: 'IotaSyncBase', component: 'HOST_LAYOUT', meta: { title: '客户端同步', icon: 'cloud-upload', roleCode: ['admin'] },
    children: [{ path: '', name: 'IotaSyncAdmin', component: SyncAdmin, meta: { title: '客户端同步', hidden: true, roleCode: ['admin'] } }] }],
  extensions: []
};
