/** @type {import('tailwindcss').Config} */
export default {
  content: [
    "./index.html",
    "./src/**/*.{js,ts,jsx,tsx}",
  ],
  theme: {
    extend: {
      colors: {
        neo: {
          green: '#00E599',
          dark: '#0A0A0A',
          gray: '#1A1A1A',
          light: '#2A2A2A',
        }
      }
    },
  },
  plugins: [],
}
