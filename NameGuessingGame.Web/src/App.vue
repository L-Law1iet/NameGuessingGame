<template>
  <div class="flex flex-col min-h-screen">
    <header class="bg-blue-600 text-white p-4">
      <div class="container mx-auto">
        <h1 class="text-xl md:text-2xl font-bold">猜名字遊戲</h1>
      </div>
    </header>
    <main class="flex-1 container mx-auto p-4">
      <router-view />
    </main>
    <footer class="bg-gray-800 text-white p-4 text-center text-sm">
      <p>猜名字遊戲 &copy; {{ new Date().getFullYear() }}</p>
    </footer>
  </div>
</template>

<script setup lang="ts">
// App component logic
</script> 