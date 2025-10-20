import { BrowserRouter, Route, Routes } from 'react-router-dom'
import Home from './pages/Home'
import WebRtcTest from './pages/WebRtcTest'

const App = () => {


	
	return <BrowserRouter>
		<Routes>
			<Route path="/" element={<Home />} />
			<Route path="/rtc" element={<WebRtcTest />} />
		</Routes>
	</BrowserRouter>
}

export default App
