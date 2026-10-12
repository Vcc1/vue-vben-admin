<script lang="ts" setup>
import type { ExpediaOverviewCard, ExpediaOverviewFilter } from '../types';

import { formatInteger } from '../formatters';

const props = defineProps<{
  activeFilter: ExpediaOverviewFilter;
  cards: ExpediaOverviewCard[];
}>();

const emit = defineEmits<{
  (e: 'toggle-filter', filter: ExpediaOverviewFilter): void;
}>();

function isActiveCard(filter: ExpediaOverviewFilter) {
  if (!filter || !props.activeFilter) {
    return false;
  }

  return (
    filter.kind === props.activeFilter.kind &&
    filter.value === props.activeFilter.value
  );
}

function handleCardClick(card: ExpediaOverviewCard) {
  if (!card.filter) {
    return;
  }
  emit('toggle-filter', card.filter);
}
</script>

<template>
  <div class="overview-grid">
    <button
      v-for="card in props.cards"
      :key="card.key"
      class="overview-card"
      :class="[`is-${card.tone}`, { 'is-active': isActiveCard(card.filter) }]"
      type="button"
      @click="handleCardClick(card)"
    >
      <div class="card-label">{{ card.label }}</div>
      <div class="card-value">{{ formatInteger(card.value) }}</div>
      <div class="card-desc">{{ card.description }}</div>
    </button>
  </div>
</template>

<style scoped>
.overview-grid {
  display: grid;
  gap: 12px;
  grid-template-columns: repeat(4, minmax(0, 1fr));
}

.overview-card {
  background: var(--el-bg-color-overlay);
  border: 1px solid var(--el-border-color-light);
  border-radius: 10px;
  min-height: 120px;
  padding: 16px;
  text-align: left;
  transition:
    border-color 0.2s ease,
    box-shadow 0.2s ease,
    transform 0.2s ease;
}

.overview-card:hover {
  transform: translateY(-1px);
}

.overview-card.is-active {
  box-shadow: 0 0 0 1px color-mix(in srgb, var(--el-color-primary) 24%, transparent);
}

.overview-card.is-primary {
  border-top: 3px solid var(--el-color-primary);
}

.overview-card.is-success {
  border-top: 3px solid var(--el-color-success);
}

.overview-card.is-warning {
  border-top: 3px solid var(--el-color-warning);
}

.overview-card.is-danger {
  border-top: 3px solid var(--el-color-danger);
}

.overview-card.is-info {
  border-top: 3px solid var(--el-color-info);
}

.card-label {
  color: var(--el-text-color-secondary);
  font-size: 13px;
  line-height: 1.5;
}

.card-value {
  color: var(--el-text-color-primary);
  font-size: 30px;
  font-weight: 700;
  margin-top: 12px;
}

.card-desc {
  color: var(--el-text-color-secondary);
  font-size: 12px;
  margin-top: 10px;
}

@media (max-width: 1440px) {
  .overview-grid {
    grid-template-columns: repeat(2, minmax(0, 1fr));
  }
}

@media (max-width: 768px) {
  .overview-grid {
    grid-template-columns: repeat(1, minmax(0, 1fr));
  }
}
</style>
