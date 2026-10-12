<script lang="ts" setup>
import type { AppViewModel } from '../../types';

import AppStatusPanel from '../../AppStatusPanel.vue';

defineProps<{
  app: AppViewModel | null;
}>();
</script>

<template>
  <AppStatusPanel :app="app" />
</template>
