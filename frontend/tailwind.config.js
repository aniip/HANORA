/** @type {import('tailwindcss').Config} */
export default {
  content: ['./index.html', './src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        coffee: {
          950: '#241711',
          900: '#38251d',
          800: '#4d3328',
          700: '#684638',
          600: '#875c43',
          500: '#a87552',
          300: '#d9b99a',
          200: '#ead9c5',
          100: '#f5eee5',
          50: '#fbf8f3',
        },
        caramel: '#c58b55',
        gold: '#e5bd91',
      },
      fontFamily: {
        sans: ['DM Sans', 'sans-serif'],
        display: ['Playfair Display', 'serif'],
      },
      boxShadow: {
        soft: '0 16px 50px rgba(55,36,25,.08)',
      },
    },
  },
  plugins: [],
}
